using System ;
using System.Collections.Generic ;
using System.Linq ;
using Arent3d.Architecture.Routing.EndPoints ;
using Arent3d.Revit ;
using Arent3d.Utility ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.DB.Mechanical ;
using Autodesk.Revit.DB.Plumbing ;


namespace Arent3d.Architecture.Routing
{
  public class RouteMEPSystem
  {
    public double DiameterTolerance { get ; }
    public double AngleTolerance { get ; }

    public MEPSystemType MEPSystemType { get ; }
    public MEPSystem? MEPSystem { get ; }
    public MEPCurveType CurveType { get ; }

    public RouteMEPSystem( Document document, SubRoute subRoute )
    {
      DiameterTolerance = document.Application.VertexTolerance ;
      AngleTolerance = document.Application.AngleTolerance ;

      MEPSystemType = subRoute.Route.GetMEPSystemType() ;
      //MEPSystem = CreateMEPSystem( document, connector, allConnectors ) ;
      MEPSystem = null ;

      CurveType = subRoute.GetMEPCurveType() ?? throw new InvalidOperationException() ;
    }

    private SizeTable<double>? _90ElbowSize ;
    public double Get90ElbowSize( double diameter )
    {
      return ( _90ElbowSize ??= new( Calculate90ElbowSize ) ).Get( diameter ) ;
    }

    private SizeTable<double>? _45ElbowSize ;
    public double Get45ElbowSize( double diameter )
    {
      return ( _45ElbowSize ??= new( Calculate45ElbowSize ) ).Get( diameter ) ;
    }

    private SizeTable<(double, double)>? _teeHeaderLength ;
    public double GetTeeHeaderLength( double headerDiameter, double branchDiameter )
    {
      if ( JunctionType.Tee == CurveType.PreferredJunctionType ) {
        return ( _teeHeaderLength ??= new( CalculateTeeHeaderLength ) ).Get( ( headerDiameter, branchDiameter ) ) ;
      }
      else {
        return branchDiameter * 0.5 ; // provisional
      }
    }

    private SizeTable<(double, double)>? _teeBranchLength ;
    public double GetTeeBranchLength( double headerDiameter, double branchDiameter )
    {
      if ( JunctionType.Tee == CurveType.PreferredJunctionType ) {
        return ( _teeBranchLength ??= new( CalculateTeeBranchLength ) ).Get( ( headerDiameter, branchDiameter ) ) ;
      }
      else {
        return headerDiameter * 0.5 ; // provisional
      }
    }

    private SizeTable<(double, double)>? _reducerLength ;
    public double GetReducerLength( double diameter1, double diameter2 )
    {
      if ( diameter1 <= 0 || diameter2 <= 0 || Math.Abs( diameter1 - diameter2 ) < DiameterTolerance ) return 0 ;

      return ( _reducerLength ??= new( CalculateReducerLength ) ).Get( ( diameter1, diameter2 ) ) ;
    }

    public double GetWeldMinDistance( double diameter )
    {
      return 1.0 / 120 ;  // 1/10 inches.
    }

    private double Calculate90ElbowSize( double diameter )
    {
      return diameter * 1.5 ; // provisional
    }
    private double Calculate45ElbowSize( double diameter )
    {
      return diameter * 1.5 ; // provisional
    }
    private double CalculateTeeHeaderLength( (double, double) value )
    {
      var (headerDiameter, branchDiameter) = value ;
      if ( headerDiameter < branchDiameter ) {
        return headerDiameter * 1.0 ; // provisional
      }
      else {
        return headerDiameter * 0.5 + branchDiameter * 0.5 ; // provisional
      }
    }
    private double CalculateTeeBranchLength( (double, double) value )
    {
      var (headerDiameter, branchDiameter) = value ;
      if ( headerDiameter < branchDiameter ) {
        return headerDiameter * 1.0 + GetReducerLength( headerDiameter, branchDiameter ) ; // provisional
      }
      else {
        return headerDiameter * 0.5 + branchDiameter * 0.5 ; // provisional
      }
    }

    public double CalculateReducerLength( (double, double) value )
    {
      var (diameter1, diameter2) = value ;

      // TODO: find reducer size

      return 0 ;
    }


    #region Get MEPSystemType

    public static MEPSystemType? GetSystemType( Document document, Connector connector )
    {
      var systemClassification = GetSystemClassificationInfo( connector ) ;

      return document.GetAllElements<MEPSystemType>().FirstOrDefault( systemClassification.IsCompatibleTo ) ;
    }

    public static bool IsCompatibleMEPSystemType( MEPSystemType type, MEPSystemClassification systemClassification )
    {
      return ( type.SystemClassification == systemClassification ) ;
    }

    private static MEPSystemClassificationInfo GetSystemClassificationInfo( Connector connector )
    {
      return MEPSystemClassificationInfo.From( connector ) ?? throw new KeyNotFoundException() ;
    }

    #endregion

    #region Create MEPSystem

    private static MEPSystem? CreateMEPSystem( Document document, Connector baseConnector, IReadOnlyCollection<Connector> allConnectors )
    {
      return baseConnector.Domain switch
      {
        Domain.DomainHvac => CreateMechanicalMEPSystem( document, baseConnector, allConnectors ),
        Domain.DomainPiping => CreatePipingMEPSystem( document, baseConnector, allConnectors ),
        _ => null,
      } ;
    }

    private static MEPSystem CreateMechanicalMEPSystem( Document document, Connector connector, IReadOnlyCollection<Connector> allConnectors )
    {
      allConnectors.ForEach( EraseOldMEPSystem ) ;
      var system = document.Create.NewMechanicalSystem( connector, allConnectors.ToConnectorSet(), connector.DuctSystemType ) ;
      SetMEPSystemParameters( system, connector ) ;
      return system ;
    }
    private static MEPSystem CreatePipingMEPSystem( Document document, Connector connector, IReadOnlyCollection<Connector> allConnectors )
    {
      allConnectors.ForEach( EraseOldMEPSystem ) ;
      var system = document.Create.NewPipingSystem( connector, allConnectors.ToConnectorSet(), connector.PipeSystemType ) ;
      SetMEPSystemParameters( system, connector ) ;
      return system ;
    }

    private static void EraseOldMEPSystem( Connector c )
    {
      if ( c.MEPSystem is not {  } mepSystem ) return ;

      if ( new ConnectorId( mepSystem.BaseEquipmentConnector ) == new ConnectorId( c ) ) {
        mepSystem.Document.Delete( mepSystem.Id ) ;
      }
      else {
        mepSystem.Remove( new[] { c }.ToConnectorSet() ) ;
        if ( mepSystem.Elements.IsEmpty ) {
          mepSystem.Document.Delete( mepSystem.Id ) ;
        }
      }
    }

    private static void SetMEPSystemParameters( MEPSystem system, Connector connector )
    {
      // TODO
    }

    #endregion

    #region Get MEPCurveType
    
    public static MEPCurveType GetMEPCurveType( Document document, IEnumerable<Connector> connectors, MEPSystemType systemType )
    {
      var diameterTolerance = document.Application.VertexTolerance ;
      HashSet<int>? available = null ;
      foreach ( var connector in connectors.Where( c => GetSystemClassificationInfo( c ).IsCompatibleTo( systemType ) ) ) {
        var (concreteType, isCompatibleType) = GetIsCompatibleFunc( connector, diameterTolerance ) ;
        var curveTypes = document.GetAllElements<MEPCurveType>( concreteType ).Where( isCompatibleType ).Select( e => e.Id.IntegerValue ) ;
        if ( null == available ) {
          available = curveTypes.ToHashSet() ;
        }
        else {
          available.IntersectWith( curveTypes ) ;
        }

        if ( 0 == available.Count ) throw new InvalidOperationException( $"Available {nameof( MEPCurveType )} is not found." ) ;
      }
      if ( null == available ) throw new InvalidOperationException( $"Available {nameof( MEPCurveType )} is not found." ) ;

      return document.GetElementById<MEPCurveType>( available.First() )! ;
    }

    private static (Type, Func<MEPCurveType, bool>) GetIsCompatibleFunc( Connector connector, double diameterTolerance )
    {
      return connector.Domain switch
      {
        Domain.DomainHvac => ( typeof( DuctType ), type => IsCompatibleDuctType( type, connector, diameterTolerance ) ),
        Domain.DomainPiping => ( typeof( PipeType ), type => IsCompatiblePipeType( type, connector, diameterTolerance ) ),
        _ => ( typeof( MEPCurveType ), type => HasCompatibleShape( type, connector, diameterTolerance ) ),
      } ;
    }

    private static bool IsCompatibleDuctType( MEPCurveType type, Connector connector, double diameterTolerance )
    {
      if ( false == HasCompatibleShape( type, connector, diameterTolerance ) ) return false ;
      if ( type is not DuctType dt ) return false ;

      return true ;
    }

    private static bool IsCompatiblePipeType( MEPCurveType type, Connector connector, double diameterTolerance )
    {
      if ( false == HasCompatibleShape( type, connector, diameterTolerance ) ) return false ;
      if ( type is not PipeType pt ) return false ;

      return true ;
    }

    private static bool HasCompatibleShape( MEPCurveType type, Connector connector, double diameterTolerance )
    {
      if ( type.Shape != connector.Shape ) return false ;

      var nominalDiameter = connector.GetDiameter() ;
      if ( false == type.HasAnyNominalDiameter( nominalDiameter, diameterTolerance ) ) return false ;
      // TODO: other parameters

      return true ;
    }

    #endregion
  }
}