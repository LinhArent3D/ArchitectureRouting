﻿using System ;
using System.Collections.Generic ;
using System.Linq ;
using Arent3d.Architecture.Routing.AppBase.Commands.Routing ;
using Arent3d.Architecture.Routing.AppBase.Forms ;
using Arent3d.Architecture.Routing.AppBase.Model ;
using Arent3d.Architecture.Routing.AppBase.ViewModel ;
using Arent3d.Architecture.Routing.Extensions ;
using Arent3d.Revit ;
using Arent3d.Revit.I18n ;
using Arent3d.Revit.UI ;
using Arent3d.Utility ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.DB.Structure ;
using Autodesk.Revit.UI ;

namespace Arent3d.Architecture.Routing.AppBase.Commands.Initialization
{
  public abstract class ShowCeedModelsCommandBase : IExternalCommand
  {
    public const string DeviceSymbolTextNoteTypeName = "Left_2.5mm_DeviceSymbolText" ;
    private const string ConditionTextNoteTypeName = "1.5mm_ConditionText" ; 
    

    protected abstract ElectricalRoutingFamilyType ElectricalRoutingFamilyType { get ; }

    public Result Execute( ExternalCommandData commandData, ref string message, ElementSet elements )
    {
      const string switch2DSymbol = "2Dシンボル切り替え" ;
      const string symbolMagnification = "シンボル倍率" ;
      const string grade3 = "グレード3" ;
      
      var doc = commandData.Application.ActiveUIDocument.Document ;
      if ( doc.ActiveView is not ViewPlan ) {
        TaskDialog.Show( "Arent", "This view is not the view plan!" ) ;
        return Result.Cancelled ;
      }
      var defaultSymbolMagnification = ImportDwgMappingModel.GetDefaultSymbolMagnification( doc ) ;

      var defaultConstructionItem = doc.GetDefaultConstructionItem() ;
      
      var viewModel = new CeedViewModel( commandData ) ;
      var dlgCeedModel = new CeedModelDialog( viewModel ) ;
      
      dlgCeedModel.ShowDialog() ;
      if ( ! ( dlgCeedModel.DialogResult ?? false ) ) 
        return Result.Cancelled ;
      
      ICollection<ElementId> groupIds = new List<ElementId>() ;
      if ( string.IsNullOrEmpty( viewModel.SelectedDeviceSymbol ) ) 
        return Result.Succeeded ;
      
      Element? element = null ;
      var result = doc.Transaction( "TransactionName.Commands.Routing.PlacementDeviceSymbol".GetAppStringByKeyOrDefault( "Placement Device Symbol" ), _ =>
      {
        var uiDoc = commandData.Application.ActiveUIDocument ;

        var point = uiDoc.Selection.PickPoint( "Connectorの配置場所を選択して下さい。" ) ;
        var condition = "屋外" ; // デフォルトの条件
        
        var symbol = doc.GetFamilySymbols( ElectricalRoutingFamilyType.Room ).FirstOrDefault() ?? throw new InvalidOperationException() ;
        var filter = new FamilyInstanceFilter( doc, symbol.Id ) ;
        var rooms = new FilteredElementCollector( doc ).WherePasses( filter ).OfType<FamilyInstance>().Where(x =>
        {
          var bb = x.get_BoundingBox( null ) ;
          var ol = new Outline( bb.Min, bb.Max ) ;
          return ol.Contains( point, GeometryHelper.Tolerance ) ;
        }).ToList() ;

        switch ( rooms.Count ) {
          case 0 :
            if ( viewModel.IsShowCondition ) {
              condition = viewModel.SelectedCondition ;
            }
            else {
              TaskDialog.Show( "Arent", "部屋の外で電気シンボルを作成することができません。部屋の中の場所を指定してください！" ) ;
              return Result.Cancelled ;
            }
            break;
          case > 1 when CreateRoomCommandBase.TryGetConditions( uiDoc.Document, out var conditions ) && conditions.Any() :
            var vm = new ArentRoomViewModel { Conditions = conditions } ;
            var view = new ArentRoomView { DataContext = vm } ;
            view.ShowDialog() ;
            if ( ! vm.IsCreate )
              return Result.Cancelled ;

            if ( viewModel.IsShowCondition && viewModel.SelectedCondition != vm.SelectedCondition ) {
              TaskDialog.Show( "Arent", "指定した条件が部屋の条件と一致していないので、再度ご確認ください。" ) ;
              return Result.Cancelled ;
            }
            
            condition = vm.SelectedCondition ;
            break ;
          case > 1 :
            TaskDialog.Show( "Arent", "指定された条件が見つかりませんでした。" ) ;
            return Result.Cancelled ;
          default :
          {
            if ( rooms.First().TryGetProperty( ElectricalRoutingElementParameter.RoomCondition, out string? value ) && !string.IsNullOrEmpty(value)) {
              if ( viewModel.IsShowCondition && viewModel.SelectedCondition != value ) {
                TaskDialog.Show( "Arent", "指定した条件が部屋の条件と一致していないので、再度ご確認ください。" ) ;
                return Result.Cancelled ;
              }
              condition = value ;
            }

            break ;
          }
        }
        
        if ( !viewModel.OriginCeedModels.Any(cmd=>cmd.Condition == condition && cmd.GeneralDisplayDeviceSymbol == viewModel.SelectedDeviceSymbol) ) {
          TaskDialog.Show( "Arent", $"We can not find any ceedmodel \"{viewModel.SelectedDeviceSymbol}\" match with this room \"{condition}\"。" ) ;
          return Result.Cancelled ;
        }

        var level = uiDoc.ActiveView.GenLevel ;
        var heightOfConnector = doc.GetHeightSettingStorable()[ level ].HeightOfConnectors.MillimetersToRevitUnits() ;
        element = GenerateConnector( uiDoc, point.X, point.Y, heightOfConnector, level, viewModel.SelectedFloorPlanType??string.Empty ) ;
        var ceedCode = string.Join( ":", viewModel.SelectedCeedCode, viewModel.SelectedDeviceSymbol, viewModel.SelectedModelNum ) ;
        if ( element is FamilyInstance familyInstance ) {
          element.SetProperty( ElectricalRoutingElementParameter.CeedCode, ceedCode ) ;
          element.SetProperty( ElectricalRoutingElementParameter.ConstructionItem, defaultConstructionItem ) ;
          familyInstance.SetConnectorFamilyType( ConnectorFamilyType.Sensor ) ;
        }

        var textTypeId = TextNoteHelper.FindOrCreateTextNoteType( doc )!.Id ;
        TextNoteOptions opts = new(textTypeId) { HorizontalAlignment = HorizontalTextAlignment.Left } ;

        var txtPosition = new XYZ( point.X , point.Y + ( 1.5 + 2 * TextNoteHelper.TextSize ).MillimetersToRevitUnits() * defaultSymbolMagnification, heightOfConnector ) ;
        var textNote = TextNote.Create( doc, doc.ActiveView.Id, txtPosition, viewModel.SelectedDeviceSymbol, opts ) ;
        doc.Regenerate();
        ElementTransformUtils.MoveElement(doc, textNote.Id, Transform.CreateTranslation(-XYZ.BasisX * 0.5 * textNote.Width * defaultSymbolMagnification).OfPoint(txtPosition) - txtPosition);

        var deviceSymbolTextNoteType = new FilteredElementCollector( doc ).OfClass( typeof( TextNoteType ) ).WhereElementIsElementType().Cast<TextNoteType>().FirstOrDefault( tt => Equals( DeviceSymbolTextNoteTypeName, tt.Name ) ) ;
        if ( deviceSymbolTextNoteType == null ) {
          var elementType = textNote.TextNoteType.Duplicate( DeviceSymbolTextNoteTypeName ) ;
          deviceSymbolTextNoteType = elementType as TextNoteType ;
          deviceSymbolTextNoteType?.get_Parameter( BuiltInParameter.TEXT_BOX_VISIBILITY ).Set( 0 ) ;
          deviceSymbolTextNoteType?.get_Parameter( BuiltInParameter.TEXT_BACKGROUND ).Set( 0 ) ;
        }

        if ( deviceSymbolTextNoteType != null ) textNote.ChangeTypeId( deviceSymbolTextNoteType.Id ) ;

        // create group of selected element and new text note
        groupIds.Add( element.Id ) ;
        groupIds.Add( textNote.Id ) ;

        return Result.Succeeded ;
      } ) ;

      if ( ! groupIds.Any() ) return result ;
      using Transaction t = new ( doc, "Create connector group." ) ;
      t.Start() ;
      if ( element != null ) {
        var isHasParameterSwitch2DSymbol = element.HasParameter( switch2DSymbol ) ;
        if ( isHasParameterSwitch2DSymbol ) element.SetProperty( switch2DSymbol, true ) ;
        var isHasParameterSymbolMagnification = element.HasParameter( symbolMagnification ) ;
        if ( isHasParameterSymbolMagnification ) element.SetProperty( symbolMagnification, defaultSymbolMagnification ) ;
        var isHasParameterGrade = element.HasParameter( grade3 ) ;
        if ( isHasParameterGrade ) element.SetProperty( grade3, doc.GetDefaultSettingStorable().GradeSettingData.GradeMode == 3 );
      }
      doc.Create.NewGroup( groupIds ) ;
      t.Commit() ;

      return result ;
    }

    private Element GenerateConnector( UIDocument uiDocument, double originX, double originY, double originZ, Level level, string floorPlanType )
    {
      FamilyInstance instance;
      if ( ! string.IsNullOrEmpty( floorPlanType ) ) {
        var connectorOneSideFamilyTypeNames = ( (ConnectorOneSideFamilyType[]) Enum.GetValues( typeof( ConnectorOneSideFamilyType ) ) ).Select( f => f.GetFieldName() ).ToHashSet() ;
        if ( connectorOneSideFamilyTypeNames.Contains( floorPlanType ) ) {
          var connectorOneSideFamilyType = GetConnectorFamilyType( floorPlanType ) ;
          var symbol = uiDocument.Document.GetFamilySymbols( connectorOneSideFamilyType ).FirstOrDefault() ?? ( uiDocument.Document.GetFamilySymbols( ElectricalRoutingFamilyType ).FirstOrDefault() ?? throw new InvalidOperationException() ) ;
          instance = symbol.Instantiate( new XYZ( originX, originY, originZ ), level, StructuralType.NonStructural ) ;
          SetIsEcoMode( uiDocument, instance );
          return instance ;
        }

        if ( new FilteredElementCollector( uiDocument.Document ).OfClass( typeof( Family ) ).FirstOrDefault( f => f.Name == floorPlanType ) is Family family ) {
          foreach ( var familySymbolId in family.GetFamilySymbolIds() ) {
            var symbol = uiDocument.Document.GetElementById<FamilySymbol>( familySymbolId ) ?? throw new InvalidOperationException() ;
            instance = symbol.Instantiate( new XYZ( originX, originY, originZ ), level, StructuralType.NonStructural ) ;
            SetIsEcoMode( uiDocument, instance );
            return instance ;
          }
        }
      }

      var routingSymbol = uiDocument.Document.GetFamilySymbols( ElectricalRoutingFamilyType ).FirstOrDefault() ?? throw new InvalidOperationException() ;
      instance = routingSymbol.Instantiate( new XYZ( originX, originY, originZ ), level, StructuralType.NonStructural ) ;
      SetIsEcoMode( uiDocument, instance );
      return instance ;
    }

    private static ConnectorOneSideFamilyType GetConnectorFamilyType( string floorPlanType )
    {
      var connectorOneSideFamilyType = ConnectorOneSideFamilyType.ConnectorOneSide1 ;
      if ( string.IsNullOrEmpty( floorPlanType ) ) return connectorOneSideFamilyType ;
      foreach ( var item in (ConnectorOneSideFamilyType[]) Enum.GetValues( typeof( ConnectorOneSideFamilyType ) ) ) {
        if ( floorPlanType == item.GetFieldName() ) connectorOneSideFamilyType = item ;
      }

      return connectorOneSideFamilyType ;
    }

    private static void SetIsEcoMode(UIDocument uiDocument, FamilyInstance instance)
    { 
      if ( false == instance.TryGetProperty( ElectricalRoutingElementParameter.IsEcoMode, out string? _ ) ) return ;
      instance.SetProperty( ElectricalRoutingElementParameter.IsEcoMode, uiDocument.Document.GetDefaultSettingStorable().EcoSettingData.IsEcoMode.ToString() ) ;
    }
  }
}