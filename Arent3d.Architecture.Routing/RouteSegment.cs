using System ;
using System.Globalization ;
using System.Text ;
using Arent3d.Architecture.Routing.RouteEnd ;
using Arent3d.Revit ;
using Autodesk.Revit.DB ;

namespace Arent3d.Architecture.Routing
{
  public class RouteSegment
  {
    public double PreferredNominalDiameter { get ; private set ; }

    public IEndPointIndicator FromId { get ; }
    public IEndPointIndicator ToId { get ; }

    public double? GetRealNominalDiameter( Document document )
    {
      if ( 0 < PreferredNominalDiameter ) return PreferredNominalDiameter ;

      return GetRealNominalDiameterFromEndPoints( document ) ;
    }

    private double? GetRealNominalDiameterFromEndPoints( Document document )
    {
      if ( FromId.GetEndPointDiameter( document ) is { } d1 ) {
        return d1 ;
      }
      if ( ToId.GetEndPointDiameter( document ) is { } d2 ) {
        return d2 ;
      }

      return null ;
    }

    public bool ApplyRealNominalDiameter( Document document )
    {
      if ( GetRealNominalDiameterFromEndPoints( document ) is not {} d ) return false ;

      PreferredNominalDiameter = d ;
      return true ;
    }

    public RouteSegment( IEndPointIndicator fromId, IEndPointIndicator toId, double preferredNominalDiameter )
    {
      PreferredNominalDiameter = preferredNominalDiameter ;
      FromId = fromId ;
      ToId = toId ;
    }
  }

  [StorableConverterOf( typeof( RouteSegment ) )]
  internal class RouteInfoConverter : StorableConverterBase<RouteSegment, string>
  {
    private static readonly char[] FieldSplitter = { '|' } ;
    
    protected override RouteSegment NativeToCustom( Element storedElement, string nativeTypeValue )
    {
      var split = nativeTypeValue.Split( FieldSplitter, StringSplitOptions.RemoveEmptyEntries ) ;
      if ( 3 != split.Length ) throw new InvalidOperationException() ;

      if ( false == double.TryParse( split[ 0 ], NumberStyles.Any, CultureInfo.InvariantCulture, out var nominalDiameter ) ) throw new InvalidOperationException() ;
      var fromId = EndPointIndicator.ParseIndicator( split[ 1 ] ) ?? throw new InvalidOperationException() ;
      var toId = EndPointIndicator.ParseIndicator( split[ 2 ] ) ?? throw new InvalidOperationException() ;

      return new RouteSegment( fromId, toId, nominalDiameter ) ;
    }

    protected override string CustomToNative( Element storedElement, RouteSegment customTypeValue )
    {
      var builder = new StringBuilder() ;

      builder.Append( customTypeValue.PreferredNominalDiameter.ToString( CultureInfo.InvariantCulture ) ) ;
      builder.Append( '|' ) ;
      builder.Append( customTypeValue.FromId ) ;
      builder.Append( '|' ) ;
      builder.Append( customTypeValue.ToId ) ;

      return builder.ToString() ;
    }
  }
}