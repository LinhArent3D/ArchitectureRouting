﻿using System ;
using System.Collections.Generic ;
using System.Collections.ObjectModel ;
using System.Linq ;
using System.Windows ;
using Arent3d.Architecture.Routing.AppBase.Commands.Routing ;
using Arent3d.Architecture.Routing.AppBase.Forms ;
using Arent3d.Architecture.Routing.AppBase.Selection ;
using Arent3d.Architecture.Routing.AppBase.ViewModel ;
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
using Autodesk.Revit.UI.Selection ;
using static Arent3d.Architecture.Routing.AppBase.Commands.Initialization.ShowElectricSymbolsCommandBase ;

namespace Arent3d.Architecture.Routing.AppBase.Commands.Initialization
{
  public abstract class CreateDetailTableCommandBase : IExternalCommand
  {
    private const string DefaultPlumbingType = "E" ;
    private const string DefaultConstructionItems = "未設定" ;
    private const string DefaultChildPlumbingSymbol = "↑" ;
    private const string NoPlumping = "配管なし" ;
    private const string NoPlumbingSize = "（なし）" ;
    public const double Percentage = 0.32 ;
    public Result Execute( ExternalCommandData commandData, ref string message, ElementSet elements )
    {
      var doc = commandData.Application.ActiveUIDocument.Document ;
      var uiDoc = commandData.Application.ActiveUIDocument ;
      var csvStorable = doc.GetCsvStorable() ;
      var wiresAndCablesModelData = csvStorable.WiresAndCablesModelData ;
      var conduitsModelData = csvStorable.ConduitsModelData ;
      var hiroiSetCdMasterNormalModelData = csvStorable.HiroiSetCdMasterNormalModelData ;
      var cnsStorable = doc.GetCnsSettingStorable() ;
      var detailSymbolStorable = doc.GetAllStorables<DetailSymbolStorable>().FirstOrDefault() ?? doc.GetDetailSymbolStorable() ;
      try {
        var pickedObjects = uiDoc.Selection.PickElementsByRectangle( ConduitSelectionFilter.Instance, "ドラックで複数コンジットを選択して下さい。" ).Where( p => p is Conduit ).ToList() ;
        var pickedObjectIds = pickedObjects.Select( p => p.UniqueId ) ;
        var (detailTableModels, isMixConstructionItems, isExistDetailTableModelRow) =
          CreateDetailTable( doc, csvStorable, detailSymbolStorable, pickedObjects, pickedObjectIds, false, true ) ;

        var conduitTypeNames = conduitsModelData.Select( c => c.PipingType ).Distinct() ;
        var conduitTypes = ( from conduitTypeName in conduitTypeNames select new DetailTableModel.ComboboxItemType( conduitTypeName, conduitTypeName ) ).ToList() ;
        conduitTypes.Add( new DetailTableModel.ComboboxItemType( NoPlumping, NoPlumping ) ) ;

        var constructionItemNames = cnsStorable.CnsSettingData.Select( d => d.CategoryName ).ToList() ;
        var constructionItems = constructionItemNames.Any()
          ? ( from constructionItemName in constructionItemNames select new DetailTableModel.ComboboxItemType( constructionItemName, constructionItemName ) )
          : new List<DetailTableModel.ComboboxItemType>() { new( DefaultConstructionItems, DefaultConstructionItems ) } ;

        var levelNames = doc.GetAllElements<Level>().OfCategory( BuiltInCategory.OST_Levels ).OrderBy( l => l.Elevation ).Select( l => l.Name ) ;
        var levels = ( from levelName in levelNames select new DetailTableModel.ComboboxItemType( levelName, levelName ) ) ;

        var wireTypeNames = wiresAndCablesModelData.Select( w => w.WireType ).Distinct() ;
        var wireTypes = ( from wireType in wireTypeNames select new DetailTableModel.ComboboxItemType( wireType, wireType ) ) ;

        var earthTypes = new List<DetailTableModel.ComboboxItemType>() { new( "IV", "IV" ), new( "EM-IE", "EM-IE" ) } ;

        var numbers = new List<DetailTableModel.ComboboxItemType>() ;
        for ( var i = 1 ; i <= 10 ; i++ ) {
          numbers.Add( new DetailTableModel.ComboboxItemType( i.ToString(), i.ToString() ) ) ;
        }

        var constructionClassificationTypeNames = hiroiSetCdMasterNormalModelData.Select( h => h.ConstructionClassification ).Distinct() ;
        var constructionClassificationTypes = ( from constructionClassification in constructionClassificationTypeNames
          select new DetailTableModel.ComboboxItemType( constructionClassification, constructionClassification ) ) ;

        var signalTypes = ( from signalType in (SignalType[]) Enum.GetValues( typeof( SignalType ) )
          select new DetailTableModel.ComboboxItemType( signalType.GetFieldName(), signalType.GetFieldName() ) ) ;

        var viewModel =
          new DetailTableViewModel(
            doc, 
            detailTableModels,
            new ObservableCollection<DetailTableModel>(),
            conduitTypes,
            constructionItems,
            levels,
            wireTypes,
            earthTypes,
            numbers,
            constructionClassificationTypes,
            signalTypes,
            conduitsModelData,
            wiresAndCablesModelData,
            isMixConstructionItems ) ;
        var dialog = new DetailTableDialog( viewModel ) ;
        dialog.ShowDialog() ;

        while ( dialog.DialogResult is false && viewModel.IsAddReference ) {
          DetailSymbolPickFilter detailSymbolFilter = new() ;
          List<string> detailSymbolIds = new() ;
          
          try {
            var pickedDetailSymbols = uiDoc.Selection.PickObjects( ObjectType.Element, detailSymbolFilter ) ;
            foreach ( var pickedDetailSymbol in pickedDetailSymbols ) {
              var detailSymbol = doc.GetAllElements<TextNote>().ToList().FirstOrDefault( x => x.Id == pickedDetailSymbol.ElementId ) ;
              if ( detailSymbol != null && ! detailSymbolIds.Contains( detailSymbol.UniqueId ) ) {
                detailSymbolIds.Add( detailSymbol.UniqueId ) ;
              }
            }

            var ( referenceDetailTableModels, _, _) = CreateDetailTable( doc, csvStorable, detailSymbolStorable, new List<Element>(), detailSymbolIds, true, true ) ;
            foreach ( var referenceDetailTableModelRow in referenceDetailTableModels ) {
              viewModel.ReferenceDetailTableModelsOrigin.Add( referenceDetailTableModelRow ) ;
            }
          }
          catch ( Autodesk.Revit.Exceptions.OperationCanceledException ) {
            // Ignore
          }
          
          dialog = new DetailTableDialog( viewModel ) ;
          dialog.ShowDialog() ;
        }

        if ( isExistDetailTableModelRow || dialog.DialogResult == true ) {
          if ( viewModel.RoutesWithConstructionItemHasChanged.Any() ) {
            var connectorGroups = UpdateConnectorAndConduitConstructionItem( doc, viewModel.RoutesWithConstructionItemHasChanged ) ;
            if ( connectorGroups.Any() ) {
              using Transaction transaction = new( doc, "Group connector" ) ;
              transaction.Start() ;
              foreach ( var (connectorId, textNoteIds) in connectorGroups ) {
                // create group for updated connector (with new property) and related text note if any
                List<ElementId> groupIds = new() { connectorId } ;
                groupIds.AddRange( textNoteIds ) ;
                doc.Create.NewGroup( groupIds ) ;
              }
              transaction.Commit() ;
            }
          }
          
          if ( viewModel.DetailSymbolIdsWithPlumbingTypeHasChanged.Any() ) {
            UpdateDetailSymbolPlumbingType( doc, detailSymbolStorable, viewModel.DetailSymbolIdsWithPlumbingTypeHasChanged ) ;
          }
        }

        if ( dialog.DialogResult ?? false ) {
          return doc.Transaction( "TransactionName.Commands.Routing.CreateDetailTable".GetAppStringByKeyOrDefault( "Set detail table" ), _ =>
          {
            if ( ! viewModel.IsCreateDetailTableOnFloorPlanView ) return Result.Succeeded ;
            var level = uiDoc.ActiveView.GenLevel ;
            var detailTableData = viewModel.DetailTableModels ;
            var scheduleName = CreateDetailTableSchedule( doc, detailTableData, level.Name ) ;
            MessageBox.Show( string.Format( "Revit.Electrical.CreateSchedule.Message".GetAppStringByKeyOrDefault( CreateScheduleSuccessfullyMessage ), scheduleName ), "Message" ) ;
            viewModel.IsCreateDetailTableOnFloorPlanView = false ;

            return Result.Succeeded ;
          } ) ;
        }

        return Result.Succeeded ;
      }
      catch {
        return Result.Cancelled ;
      }
    }

    public static string GetKeyRouting( string detailSymbolUniqueId, string fromConnectorUniqueId, string toConnectorUniqueId )
    {
      return $"{detailSymbolUniqueId},{fromConnectorUniqueId},{toConnectorUniqueId}" ;
    }

    public static string GetKeyRouting( DetailSymbolModel? detailSymbolModel )
    {
      return null == detailSymbolModel ? string.Empty : GetKeyRouting( detailSymbolModel.DetailSymbolUniqueId, detailSymbolModel.FromConnectorUniqueId, detailSymbolModel.ToConnectorUniqueId ) ;
    }

    public static string GetKeyRouting( DetailTableModel? detailTableModel )
    {
      return null == detailTableModel ? string.Empty : GetKeyRouting( detailTableModel.DetailSymbolUniqueId, detailTableModel.FromConnectorUniqueId, detailTableModel.ToConnectorUniqueId ) ;
    }

    public static ( ObservableCollection<DetailTableModel>, bool, bool ) CreateDetailTable( Document doc, CsvStorable csvStorable, DetailSymbolStorable detailSymbolStorable, List<Element> conduits, IEnumerable<string> elementUniqueIds, bool isReferenceDetailTableModels, bool isFromCreateDetailTable = false )
    {
      var hiroiSetMasterNormalModelData = csvStorable.HiroiSetMasterNormalModelData ;
      var hiroiSetMasterEcoModelData = csvStorable.HiroiSetMasterEcoModelData ;
      var hiroiMasterModelData = csvStorable.HiroiMasterModelData ;
      var hiroiSetCdMasterNormalModelData = csvStorable.HiroiSetCdMasterNormalModelData ;
      var hiroiSetCdMasterEcoModelData = csvStorable.HiroiSetCdMasterEcoModelData ;
      var ceedStorable = doc.GetAllStorables<CeedStorable>().FirstOrDefault() ;
      var detailTableModelsData = doc.GetDetailTableStorable().DetailTableModelData ;
      var detailTableModels = new ObservableCollection<DetailTableModel>() ;

      var detailSymbolModels = new List<DetailSymbolModel>() ;
      foreach ( var detailSymbol in detailSymbolStorable.DetailSymbolModelData ) {
        if ( isFromCreateDetailTable) {
          if ( detailSymbol.DetailSymbol != AddWiringInformationCommandBase.SpecialSymbol ) {
            detailSymbolModels.Add(detailSymbol);
          }
        }
        else {
          detailSymbolModels.Add(detailSymbol);
        }
      }
      
      var keyRoutingOnDetailTableModels = detailTableModelsData.Select( GetKeyRouting )
        .Distinct().ToList() ;
 
      var keyRoutingOnDetailSymbolModels = detailSymbolModels
        .Where( x => elementUniqueIds.Contains( isReferenceDetailTableModels ? x.DetailSymbolUniqueId : x.ConduitId ) 
                     && keyRoutingOnDetailTableModels.Contains( GetKeyRouting( x.DetailSymbolUniqueId, x.FromConnectorUniqueId, x.ToConnectorUniqueId ) ) )
        .Select( d => GetKeyRouting( d.DetailSymbolUniqueId, d.FromConnectorUniqueId, d.ToConnectorUniqueId ) ).Distinct().ToList() ;
      
      var isMixConstructionItems = keyRoutingOnDetailSymbolModels.Any() && CheckMixConstructionItems( detailTableModelsData, keyRoutingOnDetailSymbolModels ) ;
      
      var onlyKeyRoutingOnDetailSymbolModels = detailSymbolModels
            .Where( x => elementUniqueIds.Contains( isReferenceDetailTableModels ? x.DetailSymbolUniqueId : x.ConduitId ) 
                         && ! keyRoutingOnDetailTableModels.Contains( GetKeyRouting(x.DetailSymbolUniqueId, x.FromConnectorUniqueId, x.ToConnectorUniqueId) ) )
            .OrderBy( x => x.DetailSymbol ).ThenByDescending( x => x.IsParentSymbol )
            .GroupBy( x => GetKeyRouting(x.DetailSymbolUniqueId, x.FromConnectorUniqueId, x.ToConnectorUniqueId), ( key, p ) => new { KeyRouting = key, DetailSymbolModels = p.ToList() } ) ;
      
      foreach ( var onlyKeyRoutingOnDetailSymbolModel in onlyKeyRoutingOnDetailSymbolModels ) {
        var firstDetailSymbolModel = onlyKeyRoutingOnDetailSymbolModel.DetailSymbolModels.FirstOrDefault() ;
        var routeNames = onlyKeyRoutingOnDetailSymbolModel.DetailSymbolModels.Select( d => d.RouteName ).Distinct().ToList() ;
        var parentRouteName = firstDetailSymbolModel!.CountCableSamePosition == 1 ? firstDetailSymbolModel.RouteName : GetParentRouteName( doc, routeNames ) ;
        if ( ! string.IsNullOrEmpty( parentRouteName ) ) {
          var parentDetailSymbolModel = onlyKeyRoutingOnDetailSymbolModel.DetailSymbolModels.FirstOrDefault( d => d.RouteName == parentRouteName ) ;
          if ( isReferenceDetailTableModels ) {
            var conduitOfFirstRoute = doc.GetElement( parentDetailSymbolModel!.ConduitId ) ;
            conduits = new List<Element> { conduitOfFirstRoute } ;
          }
          
          AddDetailTableModelRow( doc, ceedStorable!, hiroiSetCdMasterNormalModelData, hiroiSetMasterNormalModelData, hiroiSetCdMasterEcoModelData, hiroiSetMasterEcoModelData, hiroiMasterModelData, csvStorable.WiresAndCablesModelData, detailTableModels, conduits, parentDetailSymbolModel!, true, isMixConstructionItems ) ;
          var routeNameArray = parentRouteName.Split( '_' ) ;
          parentRouteName = string.Join( "_", routeNameArray.First(), routeNameArray.ElementAt( 1 ) ) ;
          routeNames = routeNames.Where( n =>
          {
            var nameArray = n.Split( '_' ) ;
            var strRouteName = string.Join( "_", nameArray.First(), nameArray.ElementAt( 1 ) ) ;
            return strRouteName != parentRouteName ;
          } ).OrderByDescending( n => n ).ToList() ;
          
        }

        var childDetailSymbolModels = from routeName in routeNames select onlyKeyRoutingOnDetailSymbolModel.DetailSymbolModels.FirstOrDefault( d => d.RouteName == routeName ) ;
        foreach ( var childDetailSymbolModel in childDetailSymbolModels ) {
          if ( isReferenceDetailTableModels ) {
            var conduitOfFirstRoute = doc.GetElement( childDetailSymbolModel.ConduitId ) ;
            conduits = new List<Element> { conduitOfFirstRoute } ;
          }

          AddDetailTableModelRow( doc, ceedStorable!, hiroiSetCdMasterNormalModelData, hiroiSetMasterNormalModelData, hiroiSetCdMasterEcoModelData, hiroiSetMasterEcoModelData, hiroiMasterModelData, csvStorable.WiresAndCablesModelData, detailTableModels, conduits, childDetailSymbolModel, false, isMixConstructionItems ) ;
        }
      }

      var detailSymbolModel = conduits.Any() ? detailSymbolModels.FirstOrDefault( x => x.ConduitId.Equals( conduits.First().UniqueId ) ) : null;
      var plumbingType = null != detailSymbolModel ? detailSymbolModel.PlumbingType : DefaultPlumbingType ;
      if ( detailTableModels.Any() ) {
        SetPlumbingDataForEachWiring( detailTableModelsData, csvStorable.ConduitsModelData, ref detailTableModels, plumbingType ) ;
      }

      if ( keyRoutingOnDetailSymbolModels.Any() ) {
        var index = "-" + DateTime.Now.ToString( "yyyyMMddHHmmss.fff" ) ;
        var detailTableModelRowsOnDetailTableStorable = detailTableModelsData.Where( d => keyRoutingOnDetailSymbolModels.Contains( GetKeyRouting( d ) ) ).ToList() ;
        foreach ( var detailTableRow in detailTableModelRowsOnDetailTableStorable ) {
          if ( isReferenceDetailTableModels ) {
            if ( ! string.IsNullOrEmpty( detailTableRow.GroupId ) ) detailTableRow.GroupId += index ;
            detailTableRow.PlumbingIdentityInfo += index ;
          }
          detailTableModels.Add( detailTableRow ) ;
        }
      }

      var resultDetailTableModels = SortDetailTableModel( detailTableModels, isMixConstructionItems ) ;
      return ( resultDetailTableModels, isMixConstructionItems, keyRoutingOnDetailSymbolModels.Any() ) ;
    }

    public enum SignalType
    {
      伝送幹線,
      低電圧,
      小勢力,
      動力
    }

    public enum ConstructionClassificationType
    {
      天井隠蔽,
      天井コロガシ,
      打ち込み,
      フリーアクセス,
      露出,
      地中埋設,
      ケーブルラック配線,
      冷媒管共巻配線,
      漏水帯コロガシ,
      漏水帯配管巻,
      導圧管類
    }

    public static string CreateDetailTableSchedule( Document document, IReadOnlyCollection<DetailTableModel> detailTableModels, string levelName )
    {
      string scheduleName = "Revit.Detail.Table.Name".GetDocumentStringByKeyOrDefault( document, "Detail Table" ) + " - " + levelName + DateTime.Now.ToString( " yyyy-MM-dd HH-mm-ss" ) ;
      var detailTable = document.GetAllElements<ViewSchedule>().SingleOrDefault( v => v.Name.Contains( scheduleName ) ) ;
      if ( detailTable == null ) {
        detailTable = ViewSchedule.CreateSchedule( document, new ElementId( BuiltInCategory.OST_Conduit ) ) ;
        detailTable.Name = scheduleName ;
        detailTable.TrySetProperty( ElectricalRoutingElementParameter.ScheduleBaseName, scheduleName ) ;
      }

      InsertDetailTableDataIntoSchedule( detailTable, detailTableModels ) ;
      return scheduleName ;
    }

    private static void InsertDetailTableDataIntoSchedule( ViewSchedule viewSchedule, IReadOnlyCollection<DetailTableModel> detailTableModels )
    {
      const int columnCount = 5 ;
      const int maxCharOfCell = 4 ;
      const double minColumnWidth = 0.05 ;
      var rowData = 0 ;
      var maxCharOfWireTypeCell = 0 ;
      var maxCharOfPlumbingTypeCell = 0 ;
      var maxCharOfRemarkCell = 0 ;

      TableCellStyleOverrideOptions tableStyleOverride = new()
      {
        HorizontalAlignment = true,
        BorderLineStyle = true,
        BorderLeftLineStyle = true,
        BorderRightLineStyle = true,
        BorderTopLineStyle = false,
        BorderBottomLineStyle = false
      } ;
      TableCellStyle cellStyle = new() ;
      cellStyle.SetCellStyleOverrideOptions( tableStyleOverride ) ;
      cellStyle.FontHorizontalAlignment = HorizontalAlignmentStyle.Left ;

      TableData tableData = viewSchedule.GetTableData() ;
      TableSectionData tsdHeader = tableData.GetSectionData( SectionType.Header ) ;

      for ( var i = 0 ; i <= columnCount ; i++ ) {
        if ( i != 2 ) tsdHeader.InsertColumn( i ) ;
      }

      detailTableModels = detailTableModels.Where( d => ! string.IsNullOrEmpty( d.WireType ) && ! string.IsNullOrEmpty( d.WireSize ) ).ToList() ;
      var detailTableModelsByFloors = detailTableModels.OrderBy( d => d.Floor ).GroupBy( d => d.Floor ).Select( g => g.ToList() ).ToList() ;
      var rowCount = detailTableModels.Count + detailTableModelsByFloors.Count - 1 ;
      foreach ( var detailTableModelsByFloor in detailTableModelsByFloors ) {
        var detailTableModelsGroupByDetailSymbol = detailTableModelsByFloor.GroupBy( d => d.DetailSymbol ).ToDictionary( g => g.Key, g => g.ToList() ) ;
        rowCount += detailTableModelsGroupByDetailSymbol.Count ;
      }
      
      for ( var i = 0 ; i < rowCount ; i++ ) {
        tsdHeader.InsertRow( tsdHeader.FirstRowNumber ) ;
      }

      var isSetPipeForCoWindingWiring = false ;
      foreach ( var detailTableModelsByFloor in detailTableModelsByFloors ) {
        var detailTableModelsGroupByDetailSymbol = detailTableModelsByFloor.GroupBy( d => d.DetailSymbol ).ToDictionary( g => g.Key, g => g.ToList() ) ;
        var level = detailTableModelsByFloor.FirstOrDefault( d => ! string.IsNullOrEmpty( d.Floor ) )?.Floor ?? string.Empty ;
        tsdHeader.MergeCells( new TableMergedCell( 0, 0, 0, columnCount ) ) ;
        tsdHeader.SetCellText( rowData, 0, level + "階平面図" ) ;
        tsdHeader.SetCellStyle( rowData, 0, cellStyle ) ;
        rowData++ ;
        
        foreach ( var (detailSymbol, detailTableModelsSameWithDetailSymbol) in detailTableModelsGroupByDetailSymbol ) {
          tsdHeader.MergeCells( new TableMergedCell( rowData, 0, rowData, columnCount ) ) ;
          tsdHeader.SetCellText( rowData, 0, detailSymbol ) ;
          tsdHeader.SetCellStyle( rowData, 0, cellStyle ) ;
          rowData++ ;
          foreach ( var rowDetailTableModel in detailTableModelsSameWithDetailSymbol ) {
            var wireType = rowDetailTableModel.WireType + rowDetailTableModel.WireSize ;
            var wireStrip = string.IsNullOrEmpty( rowDetailTableModel.WireStrip ) || rowDetailTableModel.WireStrip == "-" ? "－" : "－" + rowDetailTableModel.WireStrip ;
            var wireBook = string.IsNullOrEmpty( rowDetailTableModel.WireBook ) ? "x1" : "x" + rowDetailTableModel.WireBook ;
            var (plumbingType, numberOfPlumbing) = GetPlumbingType( rowDetailTableModel.ConstructionClassification, rowDetailTableModel.PlumbingType, rowDetailTableModel.PlumbingSize, rowDetailTableModel.NumberOfPlumbing, ref isSetPipeForCoWindingWiring ) ;
            tsdHeader.SetCellText( rowData, 0, wireType ) ;
            tsdHeader.SetCellText( rowData, 1, wireStrip ) ;
            tsdHeader.SetCellText( rowData, 2, wireBook ) ;
            tsdHeader.SetCellText( rowData, 3, plumbingType ) ;
            tsdHeader.SetCellText( rowData, 4, numberOfPlumbing ) ;
            tsdHeader.SetCellText( rowData, 5, rowDetailTableModel.Remark ) ;

            if ( wireType.Length > maxCharOfWireTypeCell ) maxCharOfWireTypeCell = wireType.Length ;
            if ( plumbingType.Length > maxCharOfPlumbingTypeCell ) maxCharOfPlumbingTypeCell = plumbingType.Length ;
            if ( rowDetailTableModel.Remark.Length > maxCharOfRemarkCell ) maxCharOfRemarkCell = rowDetailTableModel.Remark.Length ;
            rowData++ ;
          }
        }
      }

      for ( var i = 0 ; i <= columnCount ; i++ ) {
        var columnWidth = i switch
        {
          0 when maxCharOfWireTypeCell > maxCharOfCell => minColumnWidth * Math.Ceiling( (double) maxCharOfWireTypeCell / maxCharOfCell ),
          3 when maxCharOfPlumbingTypeCell > maxCharOfCell => minColumnWidth * Math.Ceiling( (double) maxCharOfPlumbingTypeCell / maxCharOfCell ),
          5 when maxCharOfRemarkCell > maxCharOfCell => minColumnWidth * Math.Ceiling( (double) maxCharOfRemarkCell / maxCharOfCell ),
          _ => minColumnWidth
        } ;
        tsdHeader.SetColumnWidth( i, columnWidth ) ;
        tsdHeader.SetCellStyle( i, cellStyle ) ;
      }
      
      if ( isSetPipeForCoWindingWiring )
        MessageBox.Show( "施工区分「冷媒管共巻配線」の電線は配管が設定されているので、再度ご確認ください。", "Error" ) ;
    }

    private static ( string, string ) GetPlumbingType( string constructionClassification, string plumbingType, string plumbingSize, string numberOfPlumbing, ref bool isSetPipeForCoWindingWiring )
    {
      const string korogashi = "コロガシ" ;
      const string rack = "ラック" ;
      const string coil = "共巻" ;
      if ( plumbingType == DefaultChildPlumbingSymbol ) {
        plumbingSize = string.Empty ;
        numberOfPlumbing = string.Empty ;
      }
      else {
        plumbingType = plumbingType.Replace( DefaultChildPlumbingSymbol, "" ) ;
      }

      if ( constructionClassification == ConstructionClassificationType.天井隠蔽.GetFieldName() || constructionClassification == ConstructionClassificationType.打ち込み.GetFieldName() || constructionClassification == ConstructionClassificationType.露出.GetFieldName() || constructionClassification == ConstructionClassificationType.地中埋設.GetFieldName() ) {
        plumbingType = "(" + plumbingType + plumbingSize + ")" ;
        numberOfPlumbing = string.IsNullOrEmpty( numberOfPlumbing ) || numberOfPlumbing == "1" ? string.Empty : "x" + numberOfPlumbing ;
      }
      else if ( constructionClassification == ConstructionClassificationType.天井コロガシ.GetFieldName() || constructionClassification == ConstructionClassificationType.フリーアクセス.GetFieldName() ) {
        plumbingType = "(" + korogashi + ")" ;
        numberOfPlumbing = string.Empty ;
      }
      else if ( constructionClassification == ConstructionClassificationType.ケーブルラック配線.GetFieldName() ) {
        plumbingType = "(" + rack + ")" ;
        numberOfPlumbing = string.Empty ;
      }
      else if ( constructionClassification == ConstructionClassificationType.冷媒管共巻配線.GetFieldName() ) {
        if ( plumbingType != NoPlumping ) {
          isSetPipeForCoWindingWiring = true ;
        }
        plumbingType = "(" + coil + ")" ;
        numberOfPlumbing = string.Empty ;
      }
      else if ( constructionClassification == ConstructionClassificationType.導圧管類.GetFieldName() ) {
        plumbingType = string.IsNullOrEmpty( plumbingType ) ? string.Empty : "(" + plumbingType + plumbingSize + ")" ;
        numberOfPlumbing = string.IsNullOrEmpty( numberOfPlumbing ) || numberOfPlumbing == "1" ? string.Empty : "x" + numberOfPlumbing ;
      }
      else {
        plumbingType = string.Empty ;
        numberOfPlumbing = string.Empty ;
      }

      return ( plumbingType, numberOfPlumbing ) ;
    }

    private static ObservableCollection<DetailTableModel> SortDetailTableModel( ObservableCollection<DetailTableModel> detailTableModels, bool isMixConstructionItems )
    {
      List<DetailTableModel> sortedDetailTableModelsList = detailTableModels.ToList() ;
      var resultSortDetailModel = SortDetailModel( sortedDetailTableModelsList, isMixConstructionItems ) ;
      return new ObservableCollection<DetailTableModel>( resultSortDetailModel ) ;
    }
    
     private static List<DetailTableModel> SortDetailModel( List<DetailTableModel> detailTableModels, bool isMixConstructionItems )
     {
        List<DetailTableModel> sortedDetailTableModelsList = new() ;
        var detailTableModelsGroupByKey = detailTableModels.OrderBy( d => d.DetailSymbol ).GroupBy( GetKeyRouting ).Select( g => g.ToList() ) ;
        foreach ( var detailTableRowsGroupByKey in detailTableModelsGroupByKey ) {
          var signalTypes = (SignalType[]) Enum.GetValues( typeof( SignalType )) ;
          foreach ( var signalType in signalTypes ) {
            var detailTableRowsWithSameSignalType = detailTableRowsGroupByKey.Where( d => d.SignalType == signalType.GetFieldName() ).ToList() ;
            SortDetailTableRows( sortedDetailTableModelsList, detailTableRowsWithSameSignalType, isMixConstructionItems ) ;
          }

          var signalTypeNames = signalTypes.Select( s => s.GetFieldName() ) ;
          var detailTableRowsNotHaveSignalType = detailTableRowsGroupByKey.Where( d => ! signalTypeNames.Contains( d.SignalType ) ).ToList() ;
          SortDetailTableRows( sortedDetailTableModelsList, detailTableRowsNotHaveSignalType, isMixConstructionItems ) ;
        }

        return sortedDetailTableModelsList ;
     }
    
    private static void SortDetailTableRows( List<DetailTableModel> sortedDetailTableModelsList, List<DetailTableModel> detailTableRowsWithSameSignalType, bool isMixConstructionItems )
    {
      if ( ! isMixConstructionItems ) detailTableRowsWithSameSignalType = detailTableRowsWithSameSignalType.OrderBy( d => d.ConstructionItems ).ToList() ;
      var detailTableRowsGroupByPlumbingIdentityInfo = detailTableRowsWithSameSignalType.GroupBy( d => d.PlumbingIdentityInfo ).Select( g => g.ToList() ) ;
      foreach ( var detailTableRowsWithSamePlumbingIdentityInfo in detailTableRowsGroupByPlumbingIdentityInfo ) {
        var sortedDetailTableModels = 
            detailTableRowsWithSamePlumbingIdentityInfo
              .OrderByDescending( x => x.IsParentRoute )
              .ThenBy( x => x.GroupId ) ;

        sortedDetailTableModelsList.AddRange( sortedDetailTableModels ) ;
      }
    }

    protected internal static void SetNoPlumbingDataForOneSymbol( List<DetailTableModel> detailTableModels, bool isMixConstructionItems )
    {
      var parentDetailRow = detailTableModels.First() ;
      var plumbingIdentityInfo = string.Empty ;
      foreach ( var detailTableRow in detailTableModels ) {
        if ( detailTableRow == parentDetailRow ) {
          detailTableRow.PlumbingType = NoPlumping ;
          detailTableRow.PlumbingSize = NoPlumbingSize ;
          detailTableRow.NumberOfPlumbing = string.Empty ;
          detailTableRow.IsParentRoute = true ;
          detailTableRow.IsReadOnly = false ;
          detailTableRow.IsMixConstructionItems = isMixConstructionItems ;
          detailTableRow.IsReadOnlyPlumbingItems = ! isMixConstructionItems ;
          detailTableRow.IsReadOnlyPlumbingSize = true ;
          plumbingIdentityInfo = GetDetailTableRowPlumbingIdentityInfo( detailTableRow, isMixConstructionItems ) ;
          detailTableRow.PlumbingIdentityInfo = plumbingIdentityInfo ;
        }
        else {
          detailTableRow.PlumbingType = DefaultChildPlumbingSymbol ;
          detailTableRow.PlumbingSize = DefaultChildPlumbingSymbol ;
          detailTableRow.NumberOfPlumbing = DefaultChildPlumbingSymbol ;
          detailTableRow.IsParentRoute = false ;
          detailTableRow.IsReadOnly = true ;
          detailTableRow.IsMixConstructionItems = isMixConstructionItems ;
          detailTableRow.IsReadOnlyPlumbingItems = true ;
          detailTableRow.IsReadOnlyPlumbingSize = true ;
          detailTableRow.PlumbingIdentityInfo = plumbingIdentityInfo ;
        }
      }
    }

    protected internal static void SetPlumbingData( List<ConduitsModel> conduitsModelData, ref List<DetailTableModel> detailTableModels, string plumbingType, bool isMixConstructionItems = false )
    {
      var detailTableRowsGroupByPlumbingIdentityInfo = detailTableModels.GroupBy( d => d.PlumbingIdentityInfo ).Select( g => g.ToList() ) ;
      var detailTableRowsSinglePlumbing = new ObservableCollection<DetailTableModel>() ;
      foreach ( var detailTableRowsWithSamePlumbing in detailTableRowsGroupByPlumbingIdentityInfo ) {
        if ( detailTableRowsWithSamePlumbing.Count == 1 ) {
          detailTableRowsSinglePlumbing.Add( detailTableRowsWithSamePlumbing.First() ) ;
        }
        else {
          SetPlumbingDataForOneSymbol( conduitsModelData, detailTableRowsWithSamePlumbing, plumbingType, true, isMixConstructionItems ) ;
        }
      }

      SetPlumbingDataForEachWiring( new List<DetailTableModel>(), conduitsModelData, ref detailTableRowsSinglePlumbing, plumbingType ) ;
    }

    private static void SetPlumbingDataForEachWiring( List<DetailTableModel> detailTableModelData, List<ConduitsModel> conduitsModelData, ref ObservableCollection<DetailTableModel> detailTableModels, string plumbingType )
    {
      var newDetailTableRows = new List<DetailTableModel>() ;
      foreach ( var detailTableRow in detailTableModels ) {
        const int plumbingCount = 1 ;
        var oldDetailTableRows = detailTableModelData.Where( d => GetKeyRouting(d) == GetKeyRouting( detailTableRow ) && d.RouteName == detailTableRow.RouteName ).ToList() ;
        if ( ! oldDetailTableRows.Any() ) {
          var conduitsModels = conduitsModelData.Where( c => c.PipingType == plumbingType ).OrderBy( c => double.Parse( c.InnerCrossSectionalArea ) ).ToList() ;
          if(!conduitsModels.Any())
            continue;
          
          var maxInnerCrossSectionalArea = conduitsModels.Select( c => double.Parse( c.InnerCrossSectionalArea ) ).Max() ;
          var currentPlumbingCrossSectionalArea = detailTableRow.WireCrossSectionalArea / Percentage ;
          if ( currentPlumbingCrossSectionalArea > maxInnerCrossSectionalArea ) {
            var plumbing = conduitsModels.Last() ;
            detailTableRow.PlumbingType = plumbingType ;
            detailTableRow.PlumbingSize = plumbing!.Size.Replace( "mm", "" ) ;
          }
          else {
            var plumbing = conduitsModels.FirstOrDefault( c => double.Parse( c.InnerCrossSectionalArea ) >= currentPlumbingCrossSectionalArea ) ;
            detailTableRow.PlumbingType = plumbingType ;
            detailTableRow.PlumbingSize = plumbing!.Size.Replace( "mm", "" ) ;
          }

          detailTableRow.NumberOfPlumbing = plumbingCount.ToString() ;
          detailTableRow.PlumbingIdentityInfo = GetDetailTableRowPlumbingIdentityInfo( detailTableRow, false ) ;
          detailTableRow.IsParentRoute = true ;
          detailTableRow.IsReadOnly = false ;
          detailTableRow.IsReadOnlyPlumbingItems = true ;
          var plumbingSizesOfPlumbingType = conduitsModels.Select( c => c.Size.Replace( "mm", "" ) ).Distinct().ToList() ;
          detailTableRow.PlumbingSizes = ( from plumbingSizeType in plumbingSizesOfPlumbingType select new DetailTableModel.ComboboxItemType( plumbingSizeType, plumbingSizeType ) ).ToList() ;
        }
        else {
          var oldDetailTableRow = oldDetailTableRows.First() ;
          detailTableRow.PlumbingType = oldDetailTableRow.PlumbingType ;
          detailTableRow.PlumbingSize = oldDetailTableRow.PlumbingSize ;
          detailTableRow.NumberOfPlumbing = oldDetailTableRow.NumberOfPlumbing ;
          detailTableRow.PlumbingIdentityInfo = oldDetailTableRow.PlumbingIdentityInfo ;
          detailTableRow.IsParentRoute = oldDetailTableRow.IsParentRoute ;
          detailTableRow.IsReadOnly = oldDetailTableRow.IsReadOnly ;
          detailTableRow.IsReadOnlyPlumbingItems = oldDetailTableRow.IsReadOnlyPlumbingItems ;
          detailTableRow.IsReadOnlyPlumbingSize = oldDetailTableRow.IsReadOnlyPlumbingSize ;
          var plumbingSizesOfPlumbingType = conduitsModelData.Where( c => c.PipingType == detailTableRow.PlumbingType ).Select( c => c.Size.Replace( "mm", "" ) ).Distinct().ToList() ;
          detailTableRow.PlumbingSizes = ( from plumbingSizeType in plumbingSizesOfPlumbingType select new DetailTableModel.ComboboxItemType( plumbingSizeType, plumbingSizeType ) ).ToList() ;
          if ( oldDetailTableRows.Count <= 1 ) continue ;
          oldDetailTableRows.Remove( oldDetailTableRow ) ;
          newDetailTableRows.AddRange( oldDetailTableRows ) ;
        }
      }

      foreach ( var newDetailTableRow in newDetailTableRows ) {
        detailTableModels.Add( newDetailTableRow ) ;
      }
    }
    
    protected internal static void SetPlumbingDataForOneSymbol( List<ConduitsModel> conduitsModelData, List<DetailTableModel> detailTableModelsByDetailSymbolId, string plumbingType, bool isPlumbingTypeHasBeenChanged, bool isMixConstructionItems)
    {
      const string noPlumpingConstructionClassification = "冷媒管共巻配線" ;
      var isParentDetailRowHasTypeNoPlumbing = false ;

      var parentDetailRow = detailTableModelsByDetailSymbolId.First() ;
      if ( parentDetailRow?.ConstructionClassification == noPlumpingConstructionClassification ) {
        isParentDetailRowHasTypeNoPlumbing = true ;
      }
      
      if ( ! isPlumbingTypeHasBeenChanged ) {
        parentDetailRow = isParentDetailRowHasTypeNoPlumbing ? detailTableModelsByDetailSymbolId.First( d => d.ConstructionClassification != noPlumpingConstructionClassification ) : parentDetailRow ;
        if ( parentDetailRow != null ) plumbingType = string.IsNullOrEmpty( parentDetailRow.PlumbingType ) ? plumbingType : parentDetailRow.PlumbingType.Replace( DefaultChildPlumbingSymbol, string.Empty ) ;
      }

      if ( plumbingType == NoPlumping ) return ;
      var conduitsModels = conduitsModelData.Where( c => c.PipingType == plumbingType ).OrderBy( c => double.Parse( c.InnerCrossSectionalArea ) ).ToList() ;
      if(!conduitsModels.Any())
        return;
      
      var maxInnerCrossSectionalArea = conduitsModels.Select( c => double.Parse( c.InnerCrossSectionalArea ) ).Max() ;
      var plumbingSizesOfPlumbingType = conduitsModels.Select( c => c.Size.Replace( "mm", "" ) ).Distinct().ToList() ;
      var plumbingSizes = ( from plumbingSize in plumbingSizesOfPlumbingType select new DetailTableModel.ComboboxItemType( plumbingSize, plumbingSize ) ).ToList() ;
      var detailTableModelsBySignalType = isMixConstructionItems ? detailTableModelsByDetailSymbolId.GroupBy( d => d.SignalType ).Select( g =>  g.ToList()).ToList() : detailTableModelsByDetailSymbolId.GroupBy( d => new {d.SignalType, d.ConstructionItems} ).Select( g =>  g.ToList()).ToList();

      foreach ( var detailTableRows in detailTableModelsBySignalType ) {
        var plumbingCount = 0 ;
        Dictionary<string, List<DetailTableModel>> detailTableRowsGroupByPlumbingType = new() ;
        List<DetailTableModel> childDetailRows = new() ;
        parentDetailRow = detailTableRows.FirstOrDefault( d => d.ConstructionClassification != noPlumpingConstructionClassification ) ;
        if ( parentDetailRow == null ) continue ;
        parentDetailRow.IsParentRoute = true ;
        parentDetailRow.IsReadOnly = false ;
        var currentPlumbingCrossSectionalArea = 0.0 ;
        foreach ( var currentDetailTableRow in detailTableRows ) {
          var wireBook = string.IsNullOrEmpty( currentDetailTableRow.WireBook ) ? 1 : int.Parse( currentDetailTableRow.WireBook ) ;
          if ( currentDetailTableRow.ConstructionClassification != noPlumpingConstructionClassification ) {
            currentPlumbingCrossSectionalArea += ( currentDetailTableRow.WireCrossSectionalArea / Percentage * wireBook ) ;

            if ( currentPlumbingCrossSectionalArea > maxInnerCrossSectionalArea ) {
              var plumbing = conduitsModels.Last() ;
              if ( parentDetailRow != detailTableRows.First( d => d.ConstructionClassification != noPlumpingConstructionClassification ) ) {
                parentDetailRow.IsParentRoute = false ;
                parentDetailRow.IsReadOnly = true ;
              }
              if ( isParentDetailRowHasTypeNoPlumbing && parentDetailRow == detailTableRows.First( d => d.ConstructionClassification != noPlumpingConstructionClassification ) ) {
                parentDetailRow.PlumbingType = plumbingType ;
              }
              else {
                parentDetailRow.PlumbingType = parentDetailRow.IsParentRoute ? plumbingType : plumbingType + DefaultChildPlumbingSymbol ;
              }

              parentDetailRow.PlumbingSizes = plumbingSizes ;
              parentDetailRow.PlumbingSize = plumbing.Size.Replace( "mm", "" ) ;
              parentDetailRow.PlumbingIdentityInfo = GetDetailTableRowPlumbingIdentityInfo( parentDetailRow, isMixConstructionItems ) ;
              parentDetailRow.Remark = DetailTableViewModel.GetRemark( parentDetailRow.Remark, int.TryParse(parentDetailRow.WireBook, out var value) ? value : 1 ) ;
              parentDetailRow.IsReadOnlyPlumbingItems = ! isMixConstructionItems ;
              parentDetailRow.IsMixConstructionItems = isMixConstructionItems ;
              parentDetailRow.IsReadOnlyPlumbingSize = false ;
              if ( ! detailTableRowsGroupByPlumbingType.ContainsKey( parentDetailRow.PlumbingIdentityInfo ) )
                detailTableRowsGroupByPlumbingType.Add( parentDetailRow.PlumbingIdentityInfo, childDetailRows ) ;
              else {
                detailTableRowsGroupByPlumbingType[ parentDetailRow.PlumbingIdentityInfo ].AddRange( childDetailRows ) ;
              }

              childDetailRows = new List<DetailTableModel>() ;
              var parentWireBook = string.IsNullOrEmpty( parentDetailRow.WireBook ) ? 1 : int.Parse( parentDetailRow.WireBook ) ;
              if ( parentWireBook > 1 && parentDetailRow.WireCrossSectionalArea / Percentage * wireBook > maxInnerCrossSectionalArea ) {
                var wireCountInPlumbing = (int) ( maxInnerCrossSectionalArea / ( currentDetailTableRow.WireCrossSectionalArea / Percentage ) ) ;
                plumbingCount += (int) Math.Ceiling( (double) wireBook / wireCountInPlumbing ) ;
              }
              else {
                plumbingCount++ ;
              }
              parentDetailRow = currentDetailTableRow ;
              currentPlumbingCrossSectionalArea = currentDetailTableRow.WireCrossSectionalArea / Percentage * wireBook ;
              if ( currentDetailTableRow != detailTableRows.Last( d => d.ConstructionClassification != noPlumpingConstructionClassification ) )
                continue ;
              if ( wireBook > 1 && currentPlumbingCrossSectionalArea > maxInnerCrossSectionalArea ) {
                plumbing = conduitsModels.LastOrDefault() ;
                var wireCountInPlumbing = (int) ( maxInnerCrossSectionalArea / ( currentDetailTableRow.WireCrossSectionalArea / Percentage ) ) ;
                plumbingCount += (int) Math.Ceiling( (double) wireBook / wireCountInPlumbing ) ;
              }
              else {
                plumbing = conduitsModels.FirstOrDefault( c => double.Parse( c.InnerCrossSectionalArea ) >= currentPlumbingCrossSectionalArea ) ;
                plumbingCount++ ;
              }

              if ( currentDetailTableRow != detailTableRows.First( d => d.ConstructionClassification != noPlumpingConstructionClassification ) ) {
                currentDetailTableRow.IsParentRoute = false ;
                currentDetailTableRow.IsReadOnly = true ;
              }
              if ( isParentDetailRowHasTypeNoPlumbing && currentDetailTableRow == detailTableRows.First( d => d.ConstructionClassification != noPlumpingConstructionClassification ) ) {
                currentDetailTableRow.PlumbingType = plumbingType ;
              }
              else {
                currentDetailTableRow.PlumbingType = currentDetailTableRow == detailTableRows.First() ? plumbingType : plumbingType + DefaultChildPlumbingSymbol ;
              }
              
              currentDetailTableRow.PlumbingSizes = plumbingSizes ;
              if ( null != plumbing ) {
                currentDetailTableRow.PlumbingSize = plumbing.Size.Replace( "mm", "" ) ;
              }
              currentDetailTableRow.PlumbingIdentityInfo = GetDetailTableRowPlumbingIdentityInfo( currentDetailTableRow, isMixConstructionItems ) ;
              currentDetailTableRow.Remark = DetailTableViewModel.GetRemark( currentDetailTableRow.Remark, wireBook ) ;
              currentDetailTableRow.IsReadOnlyPlumbingItems = ! isMixConstructionItems ;
              currentDetailTableRow.IsMixConstructionItems = isMixConstructionItems ;
              currentDetailTableRow.IsReadOnlyPlumbingSize = false ;
            }
            else {
              if ( currentDetailTableRow == detailTableRows.Last( d => d.ConstructionClassification != noPlumpingConstructionClassification ) ) {
                var plumbing = conduitsModels.FirstOrDefault( c => double.Parse( c.InnerCrossSectionalArea ) >= currentPlumbingCrossSectionalArea ) ;
                if ( parentDetailRow != detailTableRows.First( d => d.ConstructionClassification != noPlumpingConstructionClassification ) ) {
                  parentDetailRow.IsParentRoute = false ;
                  parentDetailRow.IsReadOnly = true ;
                }
                if ( isParentDetailRowHasTypeNoPlumbing && parentDetailRow == detailTableRows.First( d => d.ConstructionClassification != noPlumpingConstructionClassification ) ) {
                  parentDetailRow.PlumbingType = plumbingType ;
                }
                else {
                  parentDetailRow.PlumbingType = parentDetailRow.IsParentRoute ? plumbingType : plumbingType + DefaultChildPlumbingSymbol ;
                }
                
                parentDetailRow.PlumbingSizes = plumbingSizes ;
                if ( null != plumbing ) {
                  parentDetailRow.PlumbingSize = plumbing.Size.Replace( "mm", "" ) ;
                }
                parentDetailRow.PlumbingIdentityInfo = GetDetailTableRowPlumbingIdentityInfo( parentDetailRow, isMixConstructionItems ) ;
                parentDetailRow.Remark = DetailTableViewModel.GetRemark( parentDetailRow.Remark, wireBook ) ;
                parentDetailRow.IsReadOnlyPlumbingItems = ! isMixConstructionItems ;
                parentDetailRow.IsMixConstructionItems = isMixConstructionItems ;
                parentDetailRow.IsReadOnlyPlumbingSize = false ;
                if ( ! detailTableRowsGroupByPlumbingType.ContainsKey( parentDetailRow.PlumbingIdentityInfo ) ) {
                  detailTableRowsGroupByPlumbingType.Add( parentDetailRow.PlumbingIdentityInfo, childDetailRows ) ;
                }
                else {
                  detailTableRowsGroupByPlumbingType[ parentDetailRow.PlumbingIdentityInfo ].AddRange( childDetailRows ) ;
                  detailTableRowsGroupByPlumbingType[ parentDetailRow.PlumbingIdentityInfo ].Add( currentDetailTableRow ) ;
                }

                plumbingCount++ ;
              }

              if ( currentDetailTableRow == detailTableRows.FirstOrDefault( d => d.ConstructionClassification != noPlumpingConstructionClassification ) ) continue ;
              currentDetailTableRow.PlumbingType = DefaultChildPlumbingSymbol ;
              currentDetailTableRow.PlumbingSizes = plumbingSizes ;
              currentDetailTableRow.PlumbingSize = DefaultChildPlumbingSymbol ;
              currentDetailTableRow.NumberOfPlumbing = DefaultChildPlumbingSymbol ;
              currentDetailTableRow.Remark = DetailTableViewModel.GetRemark( currentDetailTableRow.Remark, wireBook ) ;
              currentDetailTableRow.IsReadOnlyPlumbingItems = true ;
              currentDetailTableRow.IsParentRoute = false ;
              currentDetailTableRow.IsReadOnly = true ;
              currentDetailTableRow.IsMixConstructionItems = isMixConstructionItems ;
              currentDetailTableRow.IsReadOnlyPlumbingSize = true ;
              childDetailRows.Add( currentDetailTableRow ) ;
            }
          }
          else {
            currentDetailTableRow.PlumbingType = NoPlumping ;
            currentDetailTableRow.PlumbingSizes = plumbingSizes ;
            currentDetailTableRow.PlumbingSize = NoPlumbingSize ;
            currentDetailTableRow.NumberOfPlumbing = string.Empty ;
            currentDetailTableRow.Remark = DetailTableViewModel.GetRemark( currentDetailTableRow.Remark, wireBook ) ;
            currentDetailTableRow.IsReadOnly = true ;
            currentDetailTableRow.IsReadOnlyPlumbingItems = true ;
            currentDetailTableRow.IsMixConstructionItems = false ;
            currentDetailTableRow.IsReadOnlyPlumbingSize = true ;
          }
        }

        foreach ( var (plumbingIdentityInfo, detailTableRowsWithSamePlumbing) in detailTableRowsGroupByPlumbingType ) {
          foreach ( var detailTableRow in detailTableRowsWithSamePlumbing ) {
            detailTableRow.PlumbingIdentityInfo = plumbingIdentityInfo ;
          }
        }

        foreach ( var detailTableRow in detailTableRows.Where( d => d.PlumbingSize != DefaultChildPlumbingSymbol ).ToList() ) {
          detailTableRow.NumberOfPlumbing = plumbingCount.ToString() ;
        }
      }
    }

    public static string GetDetailTableRowPlumbingIdentityInfo( DetailTableModel detailTableRow, bool mixConstructionItems )
    {
      return mixConstructionItems ? 
        string.Join( "-", detailTableRow.PlumbingType + detailTableRow.PlumbingSize, detailTableRow.SignalType, detailTableRow.RouteName, GetKeyRouting(detailTableRow), detailTableRow.CopyIndex ) : 
        string.Join( "-", detailTableRow.PlumbingType + detailTableRow.PlumbingSize, detailTableRow.SignalType, detailTableRow.RouteName, GetKeyRouting(detailTableRow), detailTableRow.CopyIndex, detailTableRow.ConstructionItems ) ;
    }

    private Dictionary<ElementId, List<ElementId>> UpdateConnectorAndConduitConstructionItem( Document document, Dictionary<string, string> routesChangedConstructionItem )
    {
      Dictionary<ElementId, List<ElementId>> connectorGroups = new() ;
      List<Element> allConnector = document.GetAllElements<Element>().OfCategory( BuiltInCategorySets.OtherElectricalElements ).ToList() ;
      using Transaction transaction = new( document, "Group connector" ) ;
      transaction.Start() ;
      foreach ( var (routeName, constructionItem) in routesChangedConstructionItem ) {
        var elements = GetToConnectorAndConduitOfRoute( document, allConnector, routeName ) ;
        foreach ( var element in elements ) {
          if ( document.GetElement( element.GroupId ) is Group parentGroup ) {
            // ungroup before set property
            var attachedGroup = document.GetAllElements<Group>().Where( x => x.AttachedParentId == parentGroup.Id ) ;
            List<ElementId> listTextNoteIds = new() ;
            // ungroup textNote before ungroup connector
            foreach ( var group in attachedGroup ) {
              var ids = group.GetMemberIds() ;
              listTextNoteIds.AddRange( ids ) ;
              group.UngroupMembers() ;
            }

            connectorGroups.Add( element.Id, listTextNoteIds ) ;
            parentGroup.UngroupMembers() ;
          }

          element.SetProperty( ElectricalRoutingElementParameter.ConstructionItem, constructionItem ) ;
        }
      }

      transaction.Commit() ;

      return connectorGroups ;
    }

    private void UpdateDetailSymbolPlumbingType( Document document, DetailSymbolStorable detailSymbolStorable, Dictionary<string, string> detailSymbolsChangedPlumbingType )
    {
      using Transaction transaction = new( document, "Update Detail Symbol Data" ) ;
      transaction.Start() ;
      foreach ( var (detailSymbolId, plumbingType) in detailSymbolsChangedPlumbingType ) {
        var detailSymbolModels = detailSymbolStorable.DetailSymbolModelData.Where( d => d.DetailSymbolUniqueId == detailSymbolId ).ToList() ;
        foreach ( var detailSymbolModel in detailSymbolModels ) {
          detailSymbolModel.PlumbingType = plumbingType ;
        }
      }

      detailSymbolStorable.Save() ;
      transaction.Commit() ;
    }

    private List<Element> GetToConnectorAndConduitOfRoute( Document document, IReadOnlyCollection<Element> allConnectors, string routeName )
    {
      var conduitsAndConnectorOfRoute = document.GetAllElements<Element>().OfCategory( BuiltInCategorySets.Conduits ).Where( c => c.GetRouteName() == routeName ).ToList() ;
      foreach ( var conduit in conduitsAndConnectorOfRoute ) {
        var toEndPoint = conduit.GetNearestEndPoints( false ).ToList() ;
        if ( ! toEndPoint.Any() ) continue ;
        var toEndPointKey = toEndPoint.First().Key ;
        var toElementUniqueId = toEndPointKey.GetElementUniqueId() ;
        if ( string.IsNullOrEmpty( toElementUniqueId ) ) continue ;
        var toConnector = allConnectors.FirstOrDefault( c => c.UniqueId == toElementUniqueId ) ;
        if ( toConnector == null || toConnector.IsTerminatePoint() || toConnector.IsPassPoint() ) continue ;
        conduitsAndConnectorOfRoute.Add( toConnector ) ;
        return conduitsAndConnectorOfRoute ;
      }

      return conduitsAndConnectorOfRoute ;
    }

    private static string GetParentRouteName( Document document, List<string> routeNames )
    {
      foreach ( var routeName in routeNames ) {
        var route = document.CollectRoutes( AddInType.Electrical ).FirstOrDefault( x => x.RouteName == routeName ) ;
        if ( route == null ) continue ;
        var parentRouteName = route.GetParentBranches().ToList().LastOrDefault()?.RouteName ;
        if ( string.IsNullOrEmpty( parentRouteName ) || parentRouteName == routeName ) {
          return routeName ;
        }
      }

      return string.Empty ;
    }

    private static void AddDetailTableModelRow( Document doc, CeedStorable ceedStorable, List<HiroiSetCdMasterModel> hiroiSetCdMasterNormalModelData, List<HiroiSetMasterModel> hiroiSetMasterNormalModelData, List<HiroiSetCdMasterModel> hiroiSetCdMasterEcoModelData, List<HiroiSetMasterModel> hiroiSetMasterEcoModelData, List<HiroiMasterModel> hiroiMasterModelData, List<WiresAndCablesModel> wiresAndCablesModelData, ICollection<DetailTableModel> detailTableModels, List<Element> pickedObjects, DetailSymbolModel detailSymbolModel, bool isParentRoute, bool mixConstructionItems )
    {
      var element = pickedObjects.FirstOrDefault( p => p.UniqueId == detailSymbolModel.ConduitId ) ;
      string floor = doc.GetElementById<Level>( element!.GetLevelId() )?.Name ?? string.Empty ;
      string constructionItem = element!.LookupParameter( "Construction Item" ).AsString() ?? DefaultConstructionItems ;
      string isEcoMode = element.LookupParameter( "IsEcoMode" ).AsString() ;
      string plumbingType = detailSymbolModel.PlumbingType ;

      var ceedModel = ceedStorable.CeedModelData.FirstOrDefault( x => x.CeedSetCode == detailSymbolModel.Code && x.GeneralDisplayDeviceSymbol == detailSymbolModel.DeviceSymbol ) ;
      if ( ceedModel != null && ! string.IsNullOrEmpty( ceedModel.CeedSetCode ) && ! string.IsNullOrEmpty( ceedModel.CeedModelNumber ) ) {
        var ceedCode = ceedModel.CeedSetCode ;
        var remark = ceedModel.GeneralDisplayDeviceSymbol ;
        var hiroiCdModel = ! string.IsNullOrEmpty( isEcoMode ) && bool.Parse( isEcoMode ) ? hiroiSetCdMasterEcoModelData.FirstOrDefault( x => x.SetCode == ceedModel.CeedSetCode ) : hiroiSetCdMasterNormalModelData.FirstOrDefault( x => x.SetCode == ceedModel.CeedSetCode ) ;
        var hiroiSetModels = ! string.IsNullOrEmpty( isEcoMode ) && bool.Parse( isEcoMode ) ? hiroiSetMasterEcoModelData.Where( x => x.ParentPartModelNumber.Contains( ceedModel.CeedModelNumber ) ).Skip( 1 ) : hiroiSetMasterNormalModelData.Where( x => x.ParentPartModelNumber.Contains( ceedModel.CeedModelNumber ) ).Skip( 1 ) ;
        var constructionClassification = hiroiCdModel?.ConstructionClassification ;
        foreach ( var item in hiroiSetModels ) {
          List<string> listMaterialCode = new() ;
          if ( ! string.IsNullOrEmpty( item.MaterialCode1 ) ) listMaterialCode.Add( int.Parse( item.MaterialCode1 ).ToString() ) ;
          if ( ! string.IsNullOrEmpty( item.MaterialCode2 ) ) listMaterialCode.Add( int.Parse( item.MaterialCode2 ).ToString() ) ;
          if ( ! string.IsNullOrEmpty( item.MaterialCode3 ) ) listMaterialCode.Add( int.Parse( item.MaterialCode3 ).ToString() ) ;
          if ( ! string.IsNullOrEmpty( item.MaterialCode4 ) ) listMaterialCode.Add( int.Parse( item.MaterialCode4 ).ToString() ) ;
          if ( ! string.IsNullOrEmpty( item.MaterialCode5 ) ) listMaterialCode.Add( int.Parse( item.MaterialCode5 ).ToString() ) ;
          if ( ! string.IsNullOrEmpty( item.MaterialCode6 ) ) listMaterialCode.Add( int.Parse( item.MaterialCode6 ).ToString() ) ;
          if ( ! string.IsNullOrEmpty( item.MaterialCode7 ) ) listMaterialCode.Add( int.Parse( item.MaterialCode7 ).ToString() ) ;
          if ( ! string.IsNullOrEmpty( item.MaterialCode8 ) ) listMaterialCode.Add( int.Parse( item.MaterialCode8 ).ToString() ) ;

          if ( ! listMaterialCode.Any() ) continue ;
          var masterModels = hiroiMasterModelData.Where( x => listMaterialCode.Contains( int.Parse( x.Buzaicd ).ToString() ) ) ;
          foreach ( var master in masterModels ) {
            var wireType = master.Type ;
            var wireSize = master.Size1 ;
            var wireStrip = string.IsNullOrEmpty( master.Size2 ) || master.Size2 == "0" ? "-" : master.Size2 ;
            var wiresAndCablesModel = wiresAndCablesModelData
              .FirstOrDefault( w => 
                w.WireType == wireType 
                && w.DiameterOrNominal == wireSize
                && ( ( w.NumberOfHeartsOrLogarithm == "0" && master.Size2 == "0" ) || ( w.NumberOfHeartsOrLogarithm != "0" && master.Size2 == w.NumberOfHeartsOrLogarithm + w.COrP ) ) ) ;
            if ( wiresAndCablesModel == null ) continue ;
            var signalType = wiresAndCablesModel.Classification ;
            var wireCrossSectionalArea = double.Parse( wiresAndCablesModel.CrossSectionalArea ) ;
            
            var wireSizesOfWireType = wiresAndCablesModelData
              .Where( w => w.WireType == wireType )
              .Select( w => w.DiameterOrNominal )
              .Distinct()
              .ToList() ;
            var wireSizes = wireSizesOfWireType.Any() ? 
              ( from wireSizeType in wireSizesOfWireType select new DetailTableModel.ComboboxItemType( wireSizeType, wireSizeType ) ).ToList() 
              : new List<DetailTableModel.ComboboxItemType>() ;
            
            var wireStripsOfWireType = wiresAndCablesModelData
              .Where( w => w.WireType == wireType && w.DiameterOrNominal == wireSize )
              .Select( w => w.NumberOfHeartsOrLogarithm == "0" ? "-" : w.NumberOfHeartsOrLogarithm + w.COrP )
              .Distinct()
              .ToList() ;
            var wireStrips = wireStripsOfWireType.Any() ? 
              ( from wireStripType in wireStripsOfWireType select new DetailTableModel.ComboboxItemType( wireStripType, wireStripType ) ).ToList() 
              : new List<DetailTableModel.ComboboxItemType>() ;

            var plumbingItemTypes = new List<DetailTableModel.ComboboxItemType> { new( constructionItem, constructionItem ) } ;
            
            var detailTableRow = new DetailTableModel( 
              false, 
              floor, 
              ceedCode,
              detailSymbolModel.DetailSymbol,
              detailSymbolModel.DetailSymbolUniqueId, 
              detailSymbolModel.FromConnectorUniqueId, 
              detailSymbolModel.ToConnectorUniqueId, 
              wireType, 
              wireSize,
              wireStrip, 
              "1", 
              string.Empty, 
              string.Empty, 
              string.Empty,
              plumbingType, 
              string.Empty,
              string.Empty, 
              constructionClassification, 
              signalType, 
              constructionItem, 
              constructionItem,
              remark, 
              wireCrossSectionalArea, 
              detailSymbolModel.CountCableSamePosition,
              detailSymbolModel.RouteName,
              isEcoMode, isParentRoute, 
              ! isParentRoute, 
              string.Empty, 
              string.Empty, 
              true,
              mixConstructionItems, 
              string.Empty, 
              false, 
              false, 
              false,
              wireSizes,
              wireStrips, 
              new List<DetailTableModel.ComboboxItemType>(), 
              new List<DetailTableModel.ComboboxItemType>(), 
              plumbingItemTypes ) ;
            detailTableModels.Add( detailTableRow ) ;
          }
        }
      }
    }

    private static bool CheckMixConstructionItems( List<DetailTableModel> detailTableModelsData, List<string> keyRoutings )
    {
      var detailTableModelRowGroupMixConstructionItems = detailTableModelsData.FirstOrDefault( d => keyRoutings.Contains(GetKeyRouting(d)) && ! string.IsNullOrEmpty( d.GroupId ) && bool.Parse( d.GroupId.Split( '-' ).First() ) ) ;
      var detailTableModelRowGroupNoMixConstructionItems = detailTableModelsData.FirstOrDefault( d => keyRoutings.Contains(GetKeyRouting(d)) && ! string.IsNullOrEmpty( d.GroupId ) && ! bool.Parse( d.GroupId.Split( '-' ).First() ) ) ;
      return detailTableModelRowGroupNoMixConstructionItems == null && detailTableModelRowGroupMixConstructionItems != null ;
    }

    private class DetailSymbolPickFilter : ISelectionFilter
    {
      private const string DetailSymbolType = "DetailSymbol-TNT" ;
      public bool AllowElement( Element e )
      {
        return ( e.GetBuiltInCategory() == BuiltInCategory.OST_TextNotes && e.GroupId == ElementId.InvalidElementId && e.Name.Contains( DetailSymbolType ) ) ;
      }

      public bool AllowReference( Reference r, XYZ p )
      {
        return false ;
      }
    }
    
    #region CreateDetailTableInCaseAddWiringInfo
    public static ( ObservableCollection<DetailTableModel>, bool, bool ) CreateDetailTableAddWiringInfo( Document doc, CsvStorable csvStorable, DetailSymbolStorable detailSymbolStorable, List<Element> conduits, List<string> elementIds, bool isReferenceDetailTableModels )
    {
      var detailTable = CreateDetailTable( doc, csvStorable, detailSymbolStorable, conduits, elementIds, isReferenceDetailTableModels ) ;
      return detailTable ;
    }

    #endregion
  }
}