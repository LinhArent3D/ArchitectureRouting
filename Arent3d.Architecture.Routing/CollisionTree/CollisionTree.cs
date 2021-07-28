using System ;
using System.Collections.Generic ;
using System.Linq ;
using Arent3d.CollisionLib ;
using Arent3d.GeometryLib ;
using Arent3d.Routing ;
using Arent3d.Utility ;
using Autodesk.Revit.DB ;
using MathLib ;

namespace Arent3d.Architecture.Routing.CollisionTree
{
  public class CollisionTree : ICollisionCheck
  {
    private ITree _treeBody ;

#if DUMP_LOGS
    public IReadOnlyCollection<(ElementId ElementId, Box3d Box)> TreeElementBoxes { get ; } 

    public CollisionTree( ICollisionCheckTargetCollector collector )
    {
      TreeElementBoxes = CollectTreeElementBoxes( collector ).ToList() ;
      _treeBody = CreateTree( TreeElementBoxes.ConvertAll( box => new TreeElement( new BoxGeometryBody( box.Box ) ) ) ) ;
    }
#else
    public CollisionTree( ICollisionCheckTargetCollector collector )
    {
      _treeBody = CreateTree( CollectTreeElementBoxes( collector ).Select( tuple => new TreeElement( new BoxGeometryBody( tuple.Box ) ) ).EnumerateAll() ) ;
    }
#endif

    private static ITree CreateTree( IReadOnlyCollection<TreeElement> elms )
    {
      return CreateTreeByFactory( elms ) ;
    }

    private static IEnumerable<(ElementId ElementId, Box3d Box)> CollectTreeElementBoxes( ICollisionCheckTargetCollector collector )
    {
      foreach ( var element in collector.GetCollisionCheckTargets() ) {
        var geom = element.GetGeometryElement() ;
        if ( null == geom ) continue ;

        if ( false == collector.IsTargetGeometryElement( geom ) ) continue ;

        yield return ( element.Id, geom.GetBoundingBox().To3dRaw() ) ;
      }
    }

    private static ITree CreateTreeByFactory( IReadOnlyCollection<TreeElement> treeElements )
    {
      if ( 0 == treeElements.Count ) {
        return TreeFactory.GetTreeInstanceToBuild( TreeFactory.TreeType.Dummy, null! ) ; // Dummyの場合はtreeElementsを使用しない
      }
      else {
        var tree = TreeFactory.GetTreeInstanceToBuild( TreeFactory.TreeType.Bvh, treeElements ) ;
        tree.Build() ;
        return tree ;
      }
    }


    public IEnumerable<Box3d> GetCollidedBoxes( Box3d box )
    {
      return this._treeBody.BoxIntersects( GetGeometryBodyBox( box ) ).Select( element => element.GlobalBox3d ) ;
    }

    public IEnumerable<(Box3d, IRouteCondition?, bool)> GetCollidedBoxesInDetailToRack( Box3d box )
    {
      // Aggregated Tree から呼ぶこと、これを単独で呼ばないこと
      var tuples = this._treeBody.GetIntersectsInDetailToRack( GetGeometryBodyBox( box ) ) ;
      foreach ( var tuple in tuples ) {
        yield return ( tuple.body.GetBounds(), tuple.cond, true ) ;
      }
    }

    public IEnumerable<(Box3d, IRouteCondition?, bool)> GetCollidedBoxesAndConditions( Box3d box, bool bIgnoreStructure )
    {
      var tuples = this._treeBody.GetIntersectAndRoutingCondition( GetGeometryBodyBox( box ) ) ;
      foreach ( var tuple in tuples ) {
        if ( null != tuple.cond ) {
          yield return ( tuple.body.GetGlobalGeometryBox(), tuple.cond, false ) ;
          continue ;
        }

        foreach ( var geo in tuple.Item1.GetGlobalGeometries() ) {
          yield return ( geo.GetBounds(), null, tuple.isStructure ) ;
        }
      }
    }

    private static IGeometryBody GetGeometryBodyBox( Box3d box ) => new CollisionBox( box ) ;

    public void AddElements( IEnumerable<Element> elements )
    {
      var geometryElements = elements.Select( elm => elm.GetGeometryElement() ).NonNull() ;
      var treeElements = geometryElements.Select( geom => new TreeElement( new BoxGeometryBody( geom.GetBoundingBox().To3dRaw() ) ) ) ;
      _treeBody = TreeFactory.AddElementsOnTree( ref _treeBody, out _, treeElements ) ;
    }
  }
}