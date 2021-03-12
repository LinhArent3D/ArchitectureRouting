using System.Collections.Generic ;
using System.ComponentModel ;
using System.Linq ;
using System.Threading.Tasks ;
using Arent3d.Revit ;
using Arent3d.Revit.UI ;
using Arent3d.Utility ;
using Autodesk.Revit.Attributes ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.Exceptions ;
using Autodesk.Revit.UI ;

namespace Arent3d.Architecture.Routing.App.Commands.Routing
{
  [Transaction( TransactionMode.Manual )]
  [DisplayName( "Erase Selected Routes" )]
  [Image( "resources/MEP.ico" )]
  public class EraseSelectedRoutesCommand : RoutingCommandBase
  {
    protected override async IAsyncEnumerable<(string RouteName, RouteSegment Segment)>? GetRouteSegments( UIDocument uiDocument )
    {
      await Task.Yield() ;

      // use lazy evaluation because GetRouteSegments()'s call time is not in the transaction.
      var document = uiDocument.Document ;
      var recreatedRoutes = ThreadDispatcher.Dispatch( () =>
      {
        var selectedRoutes = Route.CollectAllDescendantBranches( SelectRoutes( uiDocument ) ) ;

        var allRoutes = Route.GetAllRelatedBranches( selectedRoutes ) ;
        allRoutes.ExceptWith( selectedRoutes ) ;
        RouteGenerator.EraseRoutes( document, selectedRoutes.Select( route => route.RouteName ), true ) ;
        return allRoutes ;
      } ) ;

      // Returns affected, but not deleted routes to recreate them.
      foreach ( var seg in recreatedRoutes.ToSegmentsWithName().EnumerateAll() ) {
        yield return seg ;
      }
    }

    private static IReadOnlyCollection<Route> SelectRoutes( UIDocument uiDocument )
    {
      var list = PointOnRoutePicker.PickedRoutesFromSelections( uiDocument ).EnumerateAll() ;
      if ( 0 < list.Count ) return list ;

      var pickInfo = PointOnRoutePicker.PickRoute( uiDocument, false, "Pick a point on a route to delete." ) ;
      return new[] { pickInfo.Route } ;
    }
  }
}