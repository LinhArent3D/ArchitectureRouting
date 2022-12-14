using System ;
using System.Collections.Generic ;
using System.Linq ;
using Arent3d.Architecture.Routing.EndPoints ;
using Arent3d.Revit ;
using Autodesk.Revit.DB ;
using MathLib ;

namespace Arent3d.Architecture.Routing.Electrical.App
{
  public class ElectricalAutoRoutingTargetGenerator : AutoRoutingTargetGenerator
  {
    private int _nextShaftEndPointIndex = 0 ;
    
    public ElectricalAutoRoutingTargetGenerator( Document document ) : base( document )
    {
    }

    protected override IReadOnlyCollection<AutoRoutingTarget> GenerateAutoRoutingTarget( IReadOnlyCollection<SubRoute> subRoutes, IReadOnlyDictionary<Route, int> priorities, IReadOnlyDictionary<SubRouteInfo, MEPSystemRouteCondition> routeConditionDictionary )
    {
      if ( 1 != subRoutes.Count ) throw new NotSupportedException() ; // subRoutes.Count must be 1 on electrical routing

      var subRoute = subRoutes.First() ;
      if ( true == string.IsNullOrEmpty( subRoute.ShaftElementUniqueId ) || Document.GetElementById<Opening>( subRoute.ShaftElementUniqueId! ) is not {} opening ) {
        return GenerateNormalAutoRoutingTarget( subRoutes, priorities, routeConditionDictionary ) ;
      }

      var fromEndPoint = subRoute.FromEndPoints.First() ;
      var toEndPoint = subRoute.ToEndPoints.First() ;
      var fromLevelId = fromEndPoint.GetLevelId( Document ) ;
      var toLevelId = toEndPoint.GetLevelId( Document ) ;
      if ( fromLevelId == toLevelId || ElementId.InvalidElementId == fromLevelId || ElementId.InvalidElementId == toLevelId ) {
        return GenerateNormalAutoRoutingTarget( subRoutes, priorities, routeConditionDictionary ) ;
      }
      if ( Document.GetElementById<Level>( fromLevelId ) is not { } fromLevel || Document.GetElementById<Level>( toLevelId ) is not { } toLevel ) {
        return GenerateNormalAutoRoutingTarget( subRoutes, priorities, routeConditionDictionary ) ;
      }

      var trueFromFixedBopHeight = subRoute.GetTrueFixedBopHeight( FixedHeightUsage.UseFromSideOnly ) ;
      var trueToFixedBopHeight = subRoute.GetTrueFixedBopHeight( FixedHeightUsage.UseToSideOnly ) ;
      var startZ = trueFromFixedBopHeight ?? fromEndPoint.RoutingStartPosition.Z ;
      var endZ = trueToFixedBopHeight ?? toEndPoint.RoutingStartPosition.Z ;
      var shaftPosition = opening.GetShaftPosition().To3dRaw().ChangeZ( ( startZ + endZ ) * 0.5 ) ;

      var priority = priorities[ subRoute.Route ] ;
      var diameter = subRoute.GetDiameter() ;
      var routeCondition = routeConditionDictionary[ new SubRouteInfo( subRoute ) ] ;

      var shaftEndPoint = new DummyBreakEndPoint( shaftPosition, new Vector3d( 0, 0, ( startZ < endZ ) ? +1 : -1 ), _nextShaftEndPointIndex ) ;
      ++_nextShaftEndPointIndex ;

      var isDirect = ( false == subRoute.IsRoutingOnPipeSpace ) ;
      var fromAutoRoutingEndPoint = new AutoRoutingEndPoint( fromEndPoint, true, null, priority, diameter, isDirect, routeCondition ) ;
      var shaftAutoRoutingEndPoint1 = new AutoRoutingEndPoint( shaftEndPoint, false, null, priority, diameter, isDirect, routeCondition ) ;
      var shaftAutoRoutingEndPoint2 = new AutoRoutingEndPoint( shaftEndPoint, true, null, priority, diameter, isDirect, routeCondition ) ;
      var toAutoRoutingEndPoint = new AutoRoutingEndPoint( toEndPoint, false, null, priority, diameter, isDirect, routeCondition ) ;

      return new[]
      {
        new AutoRoutingTarget( Document, subRoute, priority, fromAutoRoutingEndPoint, shaftAutoRoutingEndPoint1, trueFromFixedBopHeight ),
        new AutoRoutingTarget( Document, subRoute, priority, shaftAutoRoutingEndPoint2, toAutoRoutingEndPoint, trueToFixedBopHeight ),
      } ;
    }

    private IReadOnlyCollection<AutoRoutingTarget> GenerateNormalAutoRoutingTarget( IReadOnlyCollection<SubRoute> subRoutes, IReadOnlyDictionary<Route, int> priorities, IReadOnlyDictionary<SubRouteInfo, MEPSystemRouteCondition> routeConditionDictionary )
    {
      return new[] { new AutoRoutingTarget( Document, subRoutes, priorities, routeConditionDictionary ) } ;
    }
  }
}