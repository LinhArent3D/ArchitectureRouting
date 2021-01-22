using System ;
using System.Collections.Generic ;
using Arent3d.Routing ;
using MathLib ;

namespace Arent3d.Architecture.Routing.Rack
{
  public class Rack : IStructureInfo
  {
    private Box3d _box = Box3d.Null ;
    private Box2d _exactBox = Box2d.Null ;
    private Box2d _looseBox = Box2d.Null ;
    
    public Rack()
    {
      Layers = new ILayerProperty[] { new LayerProperty( this ) } ;
    }

    public IEnumerable<ILayerProperty> Layers { get ; }
    public bool IsPipeRack { get ; } = true ;
    public bool IsMainRack { get ; set ; }
    public string Name { get ; set ; } = string.Empty ;

    public Vector3d Center => _box.Center ;
    public Vector3d Size => _box.Size ;
    public double BeamInterval { get ; set ; }

    public Box3d Box
    {
      get => _box ;
      set
      {
        _box = value ;
        _exactBox = CalcExactBox( value ) ;
        _looseBox = CalcLooseBox( value ) ;
      }
    }

    private class LayerProperty : ILayerProperty
    {
      private readonly Rack _rack ;

      public LayerProperty( Rack rack )
      {
        _rack = rack ;
        PipingProperties = new[] { new PipingProperty( this ) } ;
      }

      public Vector3d Center => _rack.Center ;
      public Vector3d Size => _rack.Size ;
      public double ConnectionHeight => Center.z - Size.z * 0.5 ;
      public double BeamInterval => _rack.BeamInterval ;
      public bool IsXDirection => IsXDirectionBox( _rack._box ) ;
      public bool IsReverseDir => false ;
      public IEnumerable<IPipingProperty> PipingProperties { get ; }
      public IEnumerable<ISpaceProperty> UsedSpaces => Array.Empty<ISpaceProperty>() ;
      public IEnumerable<ISpaceProperty> SideUsedSpace => Array.Empty<ISpaceProperty>() ;
    }

    private class PipingProperty : IPipingProperty
    {
      private readonly LayerProperty _layerProperty ;

      public PipingProperty( LayerProperty layerProperty )
      {
        _layerProperty = layerProperty ;
      }

      public RangeD Range
      {
        get
        {
          if ( _layerProperty.IsXDirection ) {
            return RangeD.ConstructFromCenterHalfWidth( _layerProperty.Center.y, _layerProperty.Size.y * 0.5 ) ;
          }
          else {
            return RangeD.ConstructFromCenterHalfWidth( _layerProperty.Center.x, _layerProperty.Size.x * 0.5 ) ;
          }
        }
      }

      public LineType PrimaryPipingType => LineType.Utility ;
      public IReadOnlyCollection<LineType> ExtraPipingTypes => Array.Empty<LineType>() ;
    }

    private static bool IsXDirectionBox( Box3d box )
    {
      var (x, y, _) = box.Size ;
      return ( y <= x ) ;
    }

    public bool IsExactIntersect( Rack rack2 )
    {
      return _exactBox.IsIntersect( rack2._exactBox ) ;
    }

    public bool IsLooseIntersect( Rack rack2 )
    {
      return _looseBox.IsIntersect( rack2._looseBox ) ;
    }

    private Box2d CalcExactBox( Box3d box )
    {
      return GetBox2d( box, 2.0 * BeamInterval ) ;
    }
    private Box2d CalcLooseBox( Box3d box )
    {
      var (x, y, _) = box.Size ;
      return GetBox2d( box, 2.0 * BeamInterval, 0.5 * Math.Min( x, y ) ) ;
    }

    private Box2d GetBox2d( Box3d box, double directionalExpansion = 0.0f, double verticalExpansion = 0.0f )
    {
      return IsXDirectionBox( box )
        ? GetBoxAlongX( box, directionalExpansion, verticalExpansion )
        : GetBoxAlongY( box, directionalExpansion, verticalExpansion ) ;
    }

    private static Box2d GetBoxAlongX( Box3d b, double directionalMargin, double verticalMargin )
    {
      var size = b.Size ;
      size.x +=  ( 2 * directionalMargin ) ;
      size.y +=  ( 2 * verticalMargin ) ;
      return new Box2d( b.Center.To2d(), size.To2d() ) ;
    }

    private static Box2d GetBoxAlongY( Box3d b, double directionalMargin, double verticalMargin )
    {
      var size = b.Size ;
      size.x += (float) ( 2 * verticalMargin ) ;
      size.y += (float) ( 2 * directionalMargin ) ;
      return new Box2d( b.Center.To2d(), size.To2d() ) ;
    }
  }
}