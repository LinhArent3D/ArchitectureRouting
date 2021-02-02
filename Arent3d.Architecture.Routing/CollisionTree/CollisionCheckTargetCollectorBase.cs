using System.Collections.Generic ;
using System.Linq ;
using Autodesk.Revit.DB ;

namespace Arent3d.Architecture.Routing.CollisionTree
{
  public abstract class CollisionCheckTargetCollectorBase : ICollisionCheckTargetCollector
  {
    private readonly Document _document ;

    public CollisionCheckTargetCollectorBase( Document document )
    {
      _document = document ;
    }

    public IEnumerable<Element> GetCollisionCheckTargets()
    {
      return new FilteredElementCollector( _document ).WhereElementIsNotElementType().Where( IsCollisionCheckElement ) ;
    }

    protected abstract bool IsCollisionCheckElement( Element elm ) ;

    public bool IsTargetGeometryElement( GeometryElement gElm )
    {
      // FIXME: fake implementation
      var (min, max) = gElm.GetBoundingBox().To3d() ;

      if ( min.z < 30 || 60 < max.z ) return false ;
      if ( min.x < -20 || 100 < max.x ) return false ;
      if ( min.y < -20 || 100 < max.y ) return false ;

      return true ;
    }
  }
}