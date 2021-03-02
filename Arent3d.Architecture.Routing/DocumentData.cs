using Arent3d.Architecture.Routing.Rack ;
using Autodesk.Revit.DB ;

namespace Arent3d.Architecture.Routing
{
  public class DocumentData
  {
    /// <summary>
    /// Returns the owner <see cref="Document"/>.
    /// </summary>
    public Document Document { get ; }

    public DocumentData( Document document )
    {
      Document = document ;
    }

    /// <summary>
    /// Returns racks within the document.
    /// </summary>
    public RackCollection RackCollection { get ; } = new() ;

    /// <summary>
    /// Returns whether a route will be auto-routed on pipe racks.
    /// </summary>
    /// <param name="subRoute"></param>
    /// <returns></returns>
    public bool IsRoutingOnPipeRacks( SubRoute subRoute )
    {
      // if ( Domain.DomainHvac == subRoute.Route.Domain ) return false ;  // HVac is direct-routing only.
      //
      // return ( 0 < RackCollection.RackCount ) ;
      return false ;
    }

    /// <summary>
    /// Returns whether a route can be branched horizontally.
    /// </summary>
    /// <param name="subRoute"></param>
    /// <returns></returns>
    public bool AllowHorizontalBranches( SubRoute subRoute )
    {
      if ( Domain.DomainHvac == subRoute.Route.Domain ) return true ;

      return false ;
    }
  }
}