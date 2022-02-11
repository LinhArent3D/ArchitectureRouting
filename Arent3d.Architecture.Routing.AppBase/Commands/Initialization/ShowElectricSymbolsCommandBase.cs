﻿using System ;
using System.Collections.Generic ;
using System.Collections.ObjectModel ;
using System.IO ;
using System.Linq ;
using System.Reflection ;
using Arent3d.Architecture.Routing.AppBase.Selection ;
using Arent3d.Architecture.Routing.Extensions ;
using Arent3d.Architecture.Routing.Storable ;
using Arent3d.Architecture.Routing.Storable.Model ;
using Arent3d.Revit ;
using Arent3d.Revit.I18n ;
using Arent3d.Revit.UI ;
using Arent3d.Utility ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.DB.Electrical ;
using Autodesk.Revit.UI ;
using ImageType = Autodesk.Revit.DB.ImageType ;

namespace Arent3d.Architecture.Routing.AppBase.Commands.Initialization
{
  public class ShowElectricSymbolsCommandBase : IExternalCommand
  {
    public Result Execute( ExternalCommandData commandData, ref string message, ElementSet elements )
    {
      var doc = commandData.Application.ActiveUIDocument.Document ;
      var uiDoc = commandData.Application.ActiveUIDocument ;
      var hiroiSetMasterNormalModelData = doc.GetCsvStorable().HiroiSetMasterNormalModelData ;
      var hiroiMasterModelData = doc.GetCsvStorable().HiroiMasterModelData ;
      var ceedStorable = doc.GetAllStorables<CeedStorable>().FirstOrDefault() ;
      var pickedObjects = uiDoc.Selection.PickElementsByRectangle( ConduitSelectionFilter.Instance, "ドラックで複数コンジットを選択して下さい。" ).Where( p => p is Conduit ) ;
      var electricalSymbolModels = new ObservableCollection<ElectricalSymbolModel>() ;
      var processedDetailSymbol = new List<string>() ;
      foreach ( var element in pickedObjects ) {
        string pipingType = element.Name ;
        var existSymbolRoute = element.GetRouteName() ?? string.Empty ;
        if ( string.IsNullOrEmpty( existSymbolRoute ) || ceedStorable == null ) continue ;
        if ( processedDetailSymbol.Contains( existSymbolRoute ) ) continue ;
        string startTeminateId = string.Empty ;
        string endTeminateId = string.Empty ;
        var startPoint = element.GetNearestEndPoints( true ) ;
        var startPointKey = startPoint.FirstOrDefault()?.Key ;
        if ( startPointKey != null ) {
          startTeminateId = startPointKey.GetElementUniqueId() ;
        }

        var endPoint = element.GetNearestEndPoints( false ) ;
        var endPointKey = endPoint.FirstOrDefault()?.Key ;
        if ( endPointKey != null ) {
          endTeminateId = endPointKey!.GetElementUniqueId() ;
        }

        var (startElementId, startCeeDSymbol, startCondition, endElementId, endCeeDSymbol, endCondition) = GetFromConnectorAndToConnectorCeeDCode( doc, startTeminateId, endTeminateId ) ;
        var startCeeDModel = ceedStorable.CeedModelData.FirstOrDefault( x => x.Condition.Equals( startCondition.Trim( '\r' ) ) && x.GeneralDisplayDeviceSymbol.Equals( startCeeDSymbol.Trim( '\r' ) ) ) ;
        var endCeeDModel = ceedStorable.CeedModelData.FirstOrDefault( x => x.Condition.Equals( endCondition.Trim( '\r' ) ) && x.GeneralDisplayDeviceSymbol.Equals( endCeeDSymbol.Trim( '\r' ) ) ) ;
        var startElectricalSymbolModel = new ElectricalSymbolModel( startElementId, startCeeDModel?.FloorPlanType ?? string.Empty, startCeeDModel?.GeneralDisplayDeviceSymbol ?? string.Empty, string.Empty, string.Empty, string.Empty, pipingType, string.Empty ) ;
        var endElectricalSymbolModel = new ElectricalSymbolModel( endElementId, endCeeDModel?.FloorPlanType ?? string.Empty, endCeeDModel?.GeneralDisplayDeviceSymbol ?? string.Empty, string.Empty, string.Empty, string.Empty, pipingType, string.Empty ) ;
        processedDetailSymbol.Add( existSymbolRoute ) ;
        if ( endCeeDModel != null ) {
          var hiroiSetModels = hiroiSetMasterNormalModelData.Where( x => x.ParentPartModelNumber.Contains( endCeeDModel.CeeDModelNumber ) ).Skip( 1 ) ;
          foreach ( var item in hiroiSetModels ) {
            List<string> listMaterialCode = new List<string>() ;
            if ( ! string.IsNullOrWhiteSpace( item.MaterialCode1 ) ) {
              listMaterialCode.Add( int.Parse( item.MaterialCode1 ).ToString() ) ;
            }

            if ( ! listMaterialCode.Any() ) continue ;
            var masterModels = hiroiMasterModelData.Where( x => listMaterialCode.Contains( int.Parse( x.Buzaicd ).ToString() ) ) ;
            foreach ( var master in masterModels ) {
              startElectricalSymbolModel.WireType = master.Type ;
              startElectricalSymbolModel.WireSize = master.Size1 ;
              startElectricalSymbolModel.WireStrip = master.Size2 ;
              endElectricalSymbolModel.WireType = master.Type ;
              endElectricalSymbolModel.WireSize = master.Size1 ;
              endElectricalSymbolModel.WireStrip = master.Size2 ;
            }
          }
        }

        if ( startCeeDModel == null || endCeeDModel == null ) continue ;
        electricalSymbolModels.Add( startElectricalSymbolModel ) ;
        electricalSymbolModels.Add( endElectricalSymbolModel ) ;
      }

      SetConnectorProperties( doc, electricalSymbolModels ) ;
      return doc.Transaction( "TransactionName.Commands.Routing.ConduitInformation".GetAppStringByKeyOrDefault( "Create electrical symbol schedules" ), _ =>
      {
        CreateSchedule( doc ) ;
        return Result.Succeeded ;
      } ) ;
    }

    private static void SetConnectorProperties( Document document, ObservableCollection<ElectricalSymbolModel> electricalSymbolModels )
    {
      Dictionary<ElementId, List<ElementId>> connectorGroups = new Dictionary<ElementId, List<ElementId>>() ;
      using Transaction transaction = new Transaction( document ) ;
      transaction.Start( "Set connector's properties" ) ;
      foreach ( var item in electricalSymbolModels ) {
        var connector =  document.GetAllElements<Element>().OfCategory( BuiltInCategorySets.Connectors ).FirstOrDefault( c => c.UniqueId == item.UniqueId ) ;
        if ( connector == null ) continue ;
        UnGroupConnector( document, connector, connectorGroups ) ;
        connector.SetProperty( ConnectorFamilyParameter.DeviceSymbol, item.GeneralDisplayDeviceSymbol ) ;
        connector.SetProperty( ConnectorFamilyParameter.WiringType, $"{item.WireType + item.WireSize,-15}{"－" + item.WireStrip + " x 1",15}" ) ;
        connector.SetProperty( ConnectorFamilyParameter.InPlumbingType, item.PipingType + item.PipingSize ) ;
        var pathToImage = GetFloorPlanImagePath( item.FloorPlanSymbol ) ;
        var imageType = document.GetAllElements<ImageType>().FirstOrDefault( i => i.Path == pathToImage ) ?? ImageType.Create( document, new ImageTypeOptions( pathToImage, false, ImageTypeSource.Import ) ) ;
        Parameter param = connector.get_Parameter( BuiltInParameter.ALL_MODEL_IMAGE ) ;
        param.Set( imageType.Id ) ;
      }

      var connectorUniqueIds = electricalSymbolModels.Select( c => c.UniqueId ).ToList() ;
      var connectorsNotSchedule = document.GetAllElements<Element>().OfCategory( BuiltInCategorySets.Connectors ).Where( c => ! connectorUniqueIds.Contains( c.UniqueId ) && c is FamilyInstance ).ToList() ;
      foreach ( var connector in connectorsNotSchedule ) {
        UnGroupConnector( document, connector, connectorGroups ) ;
        connector.SetProperty( ConnectorFamilyParameter.DeviceSymbol, string.Empty ) ;
        connector.SetProperty( ConnectorFamilyParameter.WiringType, string.Empty ) ;
        connector.SetProperty( ConnectorFamilyParameter.InPlumbingType, string.Empty ) ;
      }

      transaction.Commit() ;

      using Transaction transaction2 = new Transaction( document ) ;
      transaction2.Start( "Group connector" ) ;
      foreach ( var (connectorId, textNoteIds) in connectorGroups ) {
        // create group for updated connector (with new property) and related text note if any
        List<ElementId> groupIds = new List<ElementId> { connectorId } ;
        groupIds.AddRange( textNoteIds ) ;
        document.Create.NewGroup( groupIds ) ;
      }

      transaction2.Commit() ;
    }

    private static void UnGroupConnector( Document document, Element connector, Dictionary<ElementId, List<ElementId>> connectorGroups )
    {
      var parentGroup = document.GetElement( connector.GroupId ) as Group ;
      if ( parentGroup == null ) return ;
      // ungroup before set property
      var attachedGroup = document.GetAllElements<Group>().Where( x => x.AttachedParentId == parentGroup.Id ) ;
      List<ElementId> listTextNoteIds = new List<ElementId>() ;
      // ungroup textNote before ungroup connector
      foreach ( var group in attachedGroup ) {
        var ids = @group.GetMemberIds() ;
        listTextNoteIds.AddRange( ids ) ;
        @group.UngroupMembers() ;
      }

      connectorGroups.Add( connector.Id, listTextNoteIds ) ;
      parentGroup.UngroupMembers() ;
    }

    private static (string, string, string, string, string, string) GetFromConnectorAndToConnectorCeeDCode( Document document, string fromUniqueId, string toUniqueId )
    {
      var allConnectors = document.GetAllElements<Element>().OfCategory( BuiltInCategorySets.PickUpElements ).ToList() ;

      if ( ! string.IsNullOrEmpty( fromUniqueId ) ) {
        var fromConnector = allConnectors.FirstOrDefault( c => c.UniqueId == fromUniqueId ) ;
        if ( fromConnector!.IsTerminatePoint() || fromConnector!.IsPassPoint() ) {
          fromConnector!.TryGetProperty( PassPointParameter.RelatedFromConnectorUniqueId, out string? fromConnectorUniqueId ) ;
          if ( ! string.IsNullOrEmpty( fromConnectorUniqueId ) )
            fromUniqueId= fromConnectorUniqueId! ;
        }
      }

      if ( ! string.IsNullOrEmpty( toUniqueId ) ) {
        var toConnector = allConnectors.FirstOrDefault( c => c.UniqueId == toUniqueId ) ;
        if ( toConnector!.IsTerminatePoint() || toConnector!.IsPassPoint() ) {
          toConnector!.TryGetProperty( PassPointParameter.RelatedConnectorUniqueId, out string? toConnectorUniqueId ) ;
          if ( ! string.IsNullOrEmpty( toConnectorUniqueId ) )
            toUniqueId = toConnectorUniqueId! ;
        }
      }

      var (fromGeneralSymbol, fromCondition) = GetTextFromGroup( document, allConnectors, fromUniqueId ) ;
      var (toGeneralSymbol, toCondition) = GetTextFromGroup( document, allConnectors, toUniqueId ) ;
      return ( fromUniqueId, fromGeneralSymbol, fromCondition, toUniqueId, toGeneralSymbol, toCondition ) ;
    }

    private static (string, string) GetTextFromGroup( Document document, IReadOnlyCollection<Element> allConnectors, string uniqueId )
    {
      var (result1, result2) = ( string.Empty, string.Empty ) ;
      Group? parentGroup = null ;
      var allGroup = document.GetAllElements<Group>().ToList() ;
      foreach ( var group in allGroup ) {
        var elementIds = group.GetMemberIds().ToList() ;
        var connector = allConnectors.FirstOrDefault( c => elementIds.Contains( c.Id ) && c.UniqueId == uniqueId ) ;
        if ( connector == null ) continue ;
        parentGroup = @group ;
        break ;
      }

      if ( parentGroup == null ) return ( result1, result2 ) ;
      // ungroup before set property
      var attachedGroup = document.GetAllElements<Group>().Where( x => x.AttachedParentId == parentGroup.Id ) ;
      var enumerable = attachedGroup as Group[] ?? attachedGroup.ToArray() ;
      if ( enumerable.Any() ) {
        var textNoteId = enumerable.FirstOrDefault()?.GetMemberIds().FirstOrDefault() ;
        var textNote = document.GetAllElements<TextNote>().FirstOrDefault( x => x.Id == textNoteId ) ;
        if ( textNote != null ) {
          result1 = textNote.Text ;
        }

        var textNoteId2 = enumerable.FirstOrDefault()?.GetMemberIds().Skip( 1 ).FirstOrDefault() ;
        var textNote2 = document.GetAllElements<TextNote>().FirstOrDefault( x => x.Id == textNoteId2 ) ;
        if ( textNote2 != null ) {
          result2 = textNote2.Text ;
        }
      }

      return ( result1, result2 ) ;
    }

    private static void CreateSchedule( Document document )
    {
      const string scheduleName = "Electrical Schedule" ;
      var electricalSchedule = document.GetAllElements<ViewSchedule>().FirstOrDefault( v => v.Name.Contains( scheduleName ) ) ;
      if ( electricalSchedule != null ) return ;
      electricalSchedule = ViewSchedule.CreateSchedule( document, new ElementId( BuiltInCategory.OST_ElectricalFixtures ) ) ;
      electricalSchedule.Name = scheduleName ;
      AddScheduleFields( document, electricalSchedule ) ;
      SetScheduleStyle( electricalSchedule ) ;
    }

    private static void AddScheduleFields( Document document, ViewSchedule viewSchedule )
    {
      var fields = new List<string>() { "記号", "配線", "配管（屋内）", "配管（屋外）" } ;
      List<SchedulableField> schedulableFields = new List<SchedulableField>() ;
      //Get all schedulable fields from view schedule definition.
      IList<SchedulableField> allSchedulableFields = viewSchedule.Definition.GetSchedulableFields() ;
      foreach ( SchedulableField sf in allSchedulableFields ) {
        //Get all schedule field ids
        IList<ScheduleFieldId> ids = viewSchedule.Definition.GetFieldOrder() ;
        var fieldAlreadyAdded = ids.Any( id => viewSchedule.Definition.GetField( id ).GetSchedulableField() == sf ) ;
        //If schedulable field doesn't exist in view schedule, add it.
        if ( fieldAlreadyAdded == false && ( fields.Contains( sf.GetName( document ) ) || sf.ParameterId == new ElementId( BuiltInParameter.ALL_MODEL_IMAGE ) ) ) {
          schedulableFields.Add( sf ) ;
        }
      }

      var imageField = schedulableFields.FirstOrDefault( f => f.ParameterId == new ElementId( BuiltInParameter.ALL_MODEL_IMAGE ) ) ;
      if ( imageField != null ) {
        var floorPlanSymbolField = viewSchedule.Definition.AddField( imageField ) ;
        floorPlanSymbolField.ColumnHeading = "シンボル" ;
      }

      foreach ( var schedulableField in fields.Select( field => schedulableFields.FirstOrDefault( s => s.GetName( document ) == field ) ).Where( schedulableField => schedulableField != null ) ) {
        var scheduleField = viewSchedule.Definition.AddField( schedulableField ) ;
        if ( schedulableField == null || schedulableField.GetName( document ) != "記号" ) continue ;
        var filter = new ScheduleFilter( scheduleField.FieldId, ScheduleFilterType.NotEqual, string.Empty ) ;
        viewSchedule.Definition.AddFilter( filter ) ;
      }

      viewSchedule.Definition.ShowHeaders = false ;
    }

    private static void SetScheduleStyle( ViewSchedule viewSchedule )
    {
      var fields = new List<string>() { "シンボル", "記号", "配線", "（屋内）", "（屋外）" } ;
      TableData tableData = viewSchedule.GetTableData() ;
      TableSectionData tsdHeader = tableData.GetSectionData( SectionType.Header ) ;
      TableSectionData tsdBody = tableData.GetSectionData( SectionType.Body ) ;

      for ( var i = 0 ; i < 4 ; i++ ) {
        tsdHeader.InsertColumn( i ) ;
      }

      tsdHeader.InsertRow( tsdHeader.FirstRowNumber ) ;
      tsdHeader.InsertRow( tsdHeader.FirstRowNumber ) ;

      TableCellStyleOverrideOptions options = new TableCellStyleOverrideOptions { FontSize = true, Bold = true } ;
      TableCellStyle tcs = new TableCellStyle() ;
      tcs.SetCellStyleOverrideOptions( options ) ;
      tcs.FontHorizontalAlignment = HorizontalAlignmentStyle.Left ;
      tsdHeader.SetCellStyle( 0, 0, tcs ) ;
      tsdHeader.MergeCells( new TableMergedCell( 0, 0, 0, 4 ) ) ;
      tsdHeader.SetCellText( 0, 0, "機器凡例" ) ;
      tsdHeader.MergeCells( new TableMergedCell( 1, 3, 1, 4 ) ) ;
      tsdHeader.SetCellText( 1, 3, "配管" ) ;

      for ( var i = 0 ; i < 5 ; i++ ) {
        if ( i < 3 ) tsdHeader.MergeCells( new TableMergedCell( 1, i, 2, i ) ) ;
        tsdHeader.SetCellText( i < 3 ? 1 : tsdHeader.LastRowNumber, i, fields.ElementAt( i ) ) ;
        var columnWidth = i is 2 or 3 ? 0.2 : 0.15 ;
        tsdHeader.SetColumnWidth( i, columnWidth ) ;
        tsdBody.SetColumnWidth( i, columnWidth ) ;
      }
    }

    private static string GetFloorPlanImagePath( string floorPlanType )
    {
      string fileName = "ConnectorOneSide37.png" ;
      string directory = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location ) ! ;
      var resourcesPath = Path.Combine( directory.Substring( 0, directory.IndexOf( "bin", StringComparison.Ordinal ) ), "resources" ) ;
      foreach ( var item in (ConnectorOneSideFamilyType[]) Enum.GetValues( typeof( ConnectorOneSideFamilyType ) ) ) {
        if ( floorPlanType != item.GetFieldName() ) continue ;
        fileName = item.GetFieldName() + ".png" ;
        break ;
      }

      return Path.Combine( resourcesPath, "Images", fileName ) ;
    }
  }
}