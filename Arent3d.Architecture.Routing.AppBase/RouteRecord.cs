using Autodesk.Revit.DB ;
using CsvHelper.Configuration.Attributes ;

namespace Arent3d.Architecture.Routing.AppBase
{
  /// <summary>
  /// Routing record from from-to CSV files.
  /// </summary>
  public class RouteRecord
  {
    [Index( 0 ), Name( "Route Name" )]
    public string RouteName { get ; set ; } = string.Empty ;

    [Index( 1 ), Name( "From Key" )]
    public string FromKey { get ; set ; } = string.Empty ;

    [Index( 2 ), Name( "From End Type" )]
    public string FromEndType { get ; set ; } = string.Empty ;

    [Index( 3 ), Name( "From End Param" )]
    public string FromEndParams { get ; set ; } = string.Empty ;

    [Index( 4 ), Name( "To Key" )]
    public string ToKey { get ; set ; } = string.Empty ;

    [Index( 5 ), Name( "To End Type" )]
    public string ToEndType { get ; set ; } = string.Empty ;

    [Index( 6 ), Name( "To End Param" )]
    public string ToEndParams { get ; set ; } = string.Empty ;

    [Index( 7 ), Name( "Nominal Diameter" )]
    public double? NominalDiameter { get ; set ; } = null ;

    [Index( 8 ), Name( "On Pipe Space" )]
    public bool IsRoutingOnPipeSpace { get ; set ; } = false ;

    [Index( 9 ), Name( "Preferred Curve Type Name" )]
    public string CurveTypeName { get ; set ; } = string.Empty ;

    [Index( 10 ), Name( "Preferred Height" )]
    public double? FixedBopHeight { get ; set ; } = null ;

    [Index( 11 ), Name( "Preferred AvoidType" )]
    public AvoidType AvoidType { get ; set ; } = AvoidType.Whichever ;

    [Index( 12 ), Name( "MEP System Classification" )]
    public string SystemClassification { get ; set ; } = string.Empty ;

    [Index( 13 ), Name( "MEP System Type Name" )]
    public string SystemTypeName { get ; set ; } = string.Empty ;

    [Index( 14 ), Name( "Shaft Element Id" )]
    public int ShaftElementId { get ; set ; } = -1 ;
  }
}