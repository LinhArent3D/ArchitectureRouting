﻿using System.Collections.Generic ;
using Arent3d.Architecture.Routing.AppBase.Forms ;
using Arent3d.Revit ;
using Arent3d.Revit.UI ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.UI ;
using Arent3d.Architecture.Routing.AppBase.Manager ;
using Arent3d.Architecture.Routing.AppBase.ViewModel ;


namespace Arent3d.Architecture.Routing.AppBase.Commands.Routing
{
  public abstract class PullBoxRoutingCommandBase : RoutingCommandBase<PullBoxRoutingCommandBase.PickState>
  {
    public record PickState( PointOnRoutePicker.PickInfo PickInfo, FamilyInstance PullBox, double HeightConnector, double HeightWire, XYZ RouteDirection, bool IsCreatePullBoxWithoutSettingHeight ) ;
    protected abstract ElectricalRoutingFamilyType ElectricalRoutingFamilyType { get ; }
    protected virtual ConnectorFamilyType? ConnectorType => null ;
    protected abstract AddInType GetAddInType() ;
    private bool UseConnectorDiameter() => ( AddInType.Electrical != GetAddInType() ) ;
    protected abstract string GetNameBase( MEPSystemType? systemType, MEPCurveType curveType ) ;

    protected override OperationResult<PickState> OperateUI( ExternalCommandData commandData, ElementSet elements )
    {
      var connectorHeight = ( 15.0 ).MillimetersToRevitUnits() ;
      var uiDocument = commandData.Application.ActiveUIDocument ;
      var document = uiDocument.Document ;
      var pickInfo = PointOnRoutePicker.PickRoute( uiDocument, false, "Pick point on Route", GetAddInType(), PointOnRouteFilters.RepresentativeElement ) ;
      var pullBoxViewModel = new PullBoxViewModel() ;
      var sv = new PullBoxDialog { DataContext = pullBoxViewModel } ;
      sv.ShowDialog() ;
      if ( true != sv.DialogResult ) return OperationResult<PickState>.Cancelled ;
      var (originX, originY, originZ) = pickInfo.Position ;
      var level = uiDocument.ActiveView.GenLevel ;
      var heightConnector = pullBoxViewModel.IsCreatePullBoxWithoutSettingHeight ? originZ - level.Elevation : pullBoxViewModel.HeightConnector.MillimetersToRevitUnits() ;
      var heightWire = pullBoxViewModel.IsCreatePullBoxWithoutSettingHeight ? originZ - level.Elevation : pullBoxViewModel.HeightWire.MillimetersToRevitUnits() ;

      using Transaction t = new( document, "Create pull box" ) ;
      t.Start() ;
      var pullBox = PullBoxRouteManager.GenerateConnector( document, ElectricalRoutingFamilyType, ConnectorType, originX, originY, heightConnector - connectorHeight, level, pickInfo.Route.Name ) ;
      t.Commit() ;

      return new OperationResult<PickState>( new PickState( pickInfo, pullBox, heightConnector, heightWire, pickInfo.RouteDirection, pullBoxViewModel.IsCreatePullBoxWithoutSettingHeight ) ) ;
    }

    protected override IReadOnlyCollection<(string RouteName, RouteSegment Segment)> GetRouteSegments( Document document, PickState pickState )
    {
      var (pickInfo, pullBox, heightConnector, heightWire, routeDirection, isCreatePullBoxWithoutSettingHeight ) = pickState ;
      var route = pickInfo.SubRoute.Route ;
      var systemType = route.GetMEPSystemType() ;
      var curveType = route.UniqueCurveType ;
      var nameBase = GetNameBase( systemType, curveType! ) ;
      List<string> withoutRouteNames = new() ;
      var result = PullBoxRouteManager.GetRouteSegments( document, route, pickInfo.Element, pullBox, heightConnector, heightWire, routeDirection, isCreatePullBoxWithoutSettingHeight, nameBase, withoutRouteNames ) ;

      return result ;
    }
  }
}