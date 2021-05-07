using System ;
using System.Collections.Generic ;
using System.Linq ;
using Arent3d.Architecture.Routing.EndPoints ;
using Arent3d.Revit ;
using Arent3d.Routing ;
using Arent3d.Utility ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.DB.Electrical ;
using Autodesk.Revit.DB.Mechanical ;
using Autodesk.Revit.DB.Plumbing ;
using MathLib ;

namespace Arent3d.Architecture.Routing
{
  /// <summary>
  /// Creates a mechanical system from auto routing results.
  /// </summary>
  public class MEPSystemCreator
  {
    private readonly Document _document ;
    private readonly AutoRoutingTarget _autoRoutingTarget ;
    private readonly RouteVertexToConnectorMapper _connectorMapper ;
    
    /// <summary>
    /// Returns pass-point-to-connector relation manager.
    /// </summary>
    public PassPointConnectorMapper PassPointConnectorMapper { get ; }= new() ;

    private readonly Level _level ;

    private readonly IReadOnlyDictionary<SubRoute, RouteMEPSystem> _routeMepSystemDictionary ;

    private readonly List<Connector[]> _badConnectors = new() ;

    public MEPSystemCreator( Document document, AutoRoutingTarget autoRoutingTarget, IReadOnlyDictionary<SubRoute, RouteMEPSystem> routeMepSystemDictionary )
    {
      _document = document ;
      _autoRoutingTarget = autoRoutingTarget ;
      _level = GetLevel( document, autoRoutingTarget ) ;
      _routeMepSystemDictionary = routeMepSystemDictionary ;
      _connectorMapper = new RouteVertexToConnectorMapper() ;
    }

    private static Level GetLevel( Document document, AutoRoutingTarget autoRoutingTarget )
    {
      var level = autoRoutingTarget.EndPoints.Select( endPoint => GetLevel( document, endPoint ) ).FirstOrDefault( l => l != null && l.IsValidObject ) ;
      return level ?? Level.Create( document, 0.0 ) ;
    }
    private static Level? GetLevel( Document document, AutoRoutingEndPoint endPoint )
    {
      if ( endPoint.EndPoint.GetReferenceConnector() is not { } connector ) return null ;

      return document.GetElementById<Level>( connector.Owner.LevelId ) ;
    }

    public IReadOnlyCollection<Connector[]> GetBadConnectorSet() => _badConnectors ;

    /// <summary>
    /// Registers related end route vertex and connector for an end point.
    /// </summary>
    /// <param name="routeVertex">A route vertex generated from an end point.</param>
    /// <exception cref="ArgumentException"><see cref="routeVertex"/> is not generated from an end point.</exception>
    public void RegisterEndPointConnector( IRouteVertex routeVertex )
    {
      if ( routeVertex.LineInfo.GetEndPoint() is ConnectorEndPoint cep && cep.GetConnector() is {} connector ) {
        _connectorMapper.Add( routeVertex, connector ) ;
      }
    }

    /// <summary>
    /// Registers related end route vertex and connector for an end point.
    /// </summary>
    /// <param name="routeVertex">A route vertex generated from an end point.</param>
    /// <param name="connector">A Connector generated by mep creation.</param>
    /// <param name="isFrom">Whether the vertex is a from-side connector.</param>
    /// <exception cref="InvalidOperationException"><see cref="routeVertex"/> is not generated from an end point.</exception>
    private void RegisterPassPoint( IRouteVertex routeVertex, Connector connector, bool isFrom )
    {
      if ( routeVertex is not TerminalPoint ) return ;

      switch ( routeVertex.LineInfo.GetEndPoint() ) {
        case PassPointEndPoint ep :
          PassPointConnectorMapper.Add( ep.PassPointId, isFrom, connector ) ;
          break ;
      }
    }

    /// <summary>
    /// Creates a duct from a route edge.
    /// </summary>
    /// <param name="routeEdge">A route edge.</param>
    /// <param name="passingEndPointInfo">Nearest from & to end points.</param>
    /// <returns>Newly created duct.</returns>
    public Element CreateEdgeElement( IRouteEdge routeEdge, PassingEndPointInfo passingEndPointInfo )
    {
      var startPos = _connectorMapper.GetNewConnectorPosition( routeEdge.Start, routeEdge.End ).ToXYZRaw() ;
      var endPos = _connectorMapper.GetNewConnectorPosition( routeEdge.End, routeEdge.Start ).ToXYZRaw() ;

      var baseConnector = ( routeEdge.LineInfo as AutoRoutingEndPoint )?.EndPoint.GetReferenceConnector() ?? _autoRoutingTarget.GetSubRoute( routeEdge ).GetReferenceConnector() ;
      if ( null == baseConnector ) throw new InvalidOperationException() ;

      var subRoute = _autoRoutingTarget.GetSubRoute( routeEdge ) ;
      var routeMepSystem = _routeMepSystemDictionary[ subRoute ] ;

      var element = baseConnector.Domain switch
      {
        Domain.DomainHvac => CreateDuct( startPos, endPos, routeMepSystem ),
        Domain.DomainPiping => CreatePipe( startPos, endPos, routeMepSystem ),
        Domain.DomainCableTrayConduit => CreateCableTray( startPos, endPos, routeMepSystem ),
        Domain.DomainElectrical => throw new InvalidOperationException(), // TODO
        _ => throw new InvalidOperationException(),
      } ;

      MarkAsAutoRoutedElement( element, subRoute, passingEndPointInfo ) ;

      var manager = element.GetConnectorManager() ?? throw new InvalidOperationException() ;

      var startConnector = GetConnector( manager, startPos ) ;
      var endConnector = GetConnector( manager, endPos ) ;
      startConnector.SetDiameter( subRoute.GetDiameter() ) ;
      endConnector.SetDiameter( subRoute.GetDiameter() ) ;

      element.SetRoutedElementFromToConnectorIds( new[] { startConnector.Id }, new[] { endConnector.Id } ) ;

      RegisterPassPoint( routeEdge.Start, startConnector, true ) ;
      RegisterPassPoint( routeEdge.End, endConnector, false ) ;

      _connectorMapper.Add( routeEdge.Start, startConnector ) ;
      _connectorMapper.Add( routeEdge.End, endConnector ) ;

      return element ;
    }

    private MEPCurve CreateDuct( XYZ startPos, XYZ endPos, RouteMEPSystem routeMepSystem )
    {
      var duct = Duct.Create( _document, routeMepSystem.MEPSystemType.Id, routeMepSystem.CurveType.Id, _level.Id, startPos, endPos ) ;
      if ( null != routeMepSystem.MEPSystem ) {
        duct.SetSystemType( routeMepSystem.MEPSystem.Id ) ;
      }
      return duct ;
    }
    private MEPCurve CreatePipe( XYZ startPos, XYZ endPos, RouteMEPSystem routeMepSystem )
    {
      var pipe = Pipe.Create( _document, routeMepSystem.MEPSystemType.Id, routeMepSystem.CurveType.Id, _level.Id, startPos, endPos ) ;
      if ( null != routeMepSystem.MEPSystem ) {
        pipe.SetSystemType( routeMepSystem.MEPSystem.Id ) ;
      }
      return pipe ;
    }
    private MEPCurve CreateCableTray( XYZ startPos, XYZ endPos, RouteMEPSystem routeMepSystem )
    {
      return CableTray.Create( _document, routeMepSystem.CurveType.Id, startPos, endPos, _level.Id ) ;
    }

    private static Connector GetConnector( ConnectorManager connectorManager, XYZ position )
    {
      foreach ( Connector conn in connectorManager.Connectors ) {
        if ( conn.ConnectorType == ConnectorType.Logical ) continue ;
        if ( conn.Origin.IsAlmostEqualTo( position ) ) return conn ;
      }

      throw new InvalidOperationException() ;
    }

    /// <summary>
    /// Connect all connectors related to each route vertex.
    /// </summary>
    public void ConnectAllVertices()
    {
      foreach ( var connectors in _connectorMapper.GetConnections( _document ) ) {
        ConnectConnectors( connectors ) ;
      }
    }

    /// <summary>
    /// Connect all connectors.
    /// </summary>
    /// <param name="connectors">Connectors to be connected</param>
    private void ConnectConnectors( IReadOnlyList<Connector> connectors )
    {
      switch ( connectors.Count ) {
        case 1 : return ;
        case 2 :
          ConnectTwoConnectors( connectors[ 0 ], connectors[ 1 ] ) ;
          break ;
        case 3 :
          ConnectThreeConnectors( connectors[ 0 ], connectors[ 1 ], connectors[ 2 ] ) ;
          break ;
        case 4 :
          ConnectFourConnectors( connectors[ 0 ], connectors[ 1 ], connectors[ 2 ], connectors[ 3 ] ) ;
          break ;
        default : throw new InvalidOperationException() ;
      }
    }

    /// <summary>
    /// Connect all connectors.
    /// </summary>
    /// <param name="document">Document of the connectors.</param>
    /// <param name="connectors">Connectors to be connected</param>
    public static (bool Success, Element? Fitting) ConnectConnectors( Document document, IReadOnlyList<Connector> connectors )
    {
      switch ( connectors.Count ) {
        case 1 : return ( false, null ) ;
        case 2 : return ConnectTwoConnectors( document, connectors[ 0 ], connectors[ 1 ] ) ;
        case 3 : return ConnectThreeConnectors( document, connectors[ 0 ], connectors[ 1 ], connectors[ 2 ] ) ;
        case 4 : return ConnectFourConnectors( document, connectors[ 0 ], connectors[ 1 ], connectors[ 2 ], connectors[ 3 ] ) ;
        default : return ( false, null ) ;
      }
    }

    /// <summary>
    /// Connect two connectors. Elbow is inserted if needed.
    /// </summary>
    /// <param name="document"></param>
    /// <param name="connector1"></param>
    /// <param name="connector2"></param>
    /// <returns>
    /// <para>Success: false if error occurs.</para>
    /// <para>Fitting: Fitting if inserted.</para>
    /// </returns>
    private static (bool Success, FamilyInstance? Fitting) ConnectTwoConnectors( Document document, Connector connector1, Connector connector2 )
    {
      using var connectorTransaction = new SubTransaction( document ) ;
      try {
        connectorTransaction.Start() ;
        connector1.ConnectTo( connector2 ) ;
        connectorTransaction.Commit() ;
      }
      catch {
        connectorTransaction.RollBack() ;
        return ( false, null ) ;
      }

      var dir1 = connector1.CoordinateSystem.BasisZ.To3dRaw() ;
      var dir2 = connector2.CoordinateSystem.BasisZ.To3dRaw() ;

      if ( 0.9 < Math.Abs( Vector3d.Dot( dir1, dir2 ) ) ) {
        // Connect directly(-1) or bad connection(+1)
        return ( true, null ) ;
      }
      else {
        // Orthogonal
        using var transaction = new SubTransaction( document ) ;
        try {
          transaction.Start() ;
          var family = document.Create.NewElbowFitting( connector1, connector2 ) ;
          if ( HasReverseConnectorDirection( family ) ) throw new Exception() ;
          transaction.Commit() ;

          return ( true, family ) ;
        }
        catch {
          transaction.RollBack() ;
          return ( false, null ) ;
        }
      }
    }

    /// <summary>
    /// Connect three connectors. Tee is inserted if needed.
    /// </summary>
    /// <param name="document"></param>
    /// <param name="connector1"></param>
    /// <param name="connector2"></param>
    /// <param name="connector3"></param>
    /// <returns>
    /// <para>Success: false if error occurs.</para>
    /// <para>Fitting: Fitting if inserted.</para>
    /// </returns>
    private static (bool Success, FamilyInstance? Fitting) ConnectThreeConnectors( Document document, Connector connector1, Connector connector2, Connector connector3 )
    {
      var z1 = connector1.CoordinateSystem.BasisZ ;
      var z2 = connector2.CoordinateSystem.BasisZ ;
      var z3 = connector3.CoordinateSystem.BasisZ ;
      if ( IsOpposite( z1, z3 ) ) {
        ( connector2, connector3 ) = ( connector3, connector2 ) ;
      }
      else if ( IsOpposite( z2, z3 ) ) {
        ( connector1, connector2, connector3 ) = ( connector2, connector3, connector1 ) ;
      }
      else if ( false == IsOpposite( z1, z2 ) ) {
        return ( false, null ) ;
      }

      using var connectorTransaction = new SubTransaction( document ) ;
      try {
        connectorTransaction.Start() ;
        connector1.ConnectTo( connector2 ) ;
        connector1.ConnectTo( connector3 ) ;
        connector2.ConnectTo( connector3 ) ;
        connectorTransaction.Commit() ;
      }
      catch {
        connectorTransaction.RollBack() ;
        return ( false, null ) ;
      }

      using var transaction = new SubTransaction( document ) ;
      try {
        transaction.Start() ;
        var family = document.Create.NewTeeFitting( connector1, connector2, connector3 ) ;
        if ( HasReverseConnectorDirection( family ) ) throw new Exception() ;
        transaction.Commit() ;

        return ( true, family ) ;
      }
      catch {
        transaction.RollBack() ;
        return ( false, null ) ;
      }
    }

    /// <summary>
    /// Connect four connectors. Cross is inserted if needed.
    /// </summary>
    /// <param name="document"></param>
    /// <param name="connector1"></param>
    /// <param name="connector2"></param>
    /// <param name="connector3"></param>
    /// <param name="connector4"></param>
    /// <returns>
    /// <para>Success: false if error occurs.</para>
    /// <para>Fitting: Fitting if inserted.</para>
    /// </returns>
    private static (bool Success, FamilyInstance? Fitting) ConnectFourConnectors( Document document, Connector connector1, Connector connector2, Connector connector3, Connector connector4 )
    {
      var z1 = connector1.CoordinateSystem.BasisZ ;
      var z2 = connector2.CoordinateSystem.BasisZ ;
      var z3 = connector3.CoordinateSystem.BasisZ ;
      var z4 = connector4.CoordinateSystem.BasisZ ;
      if ( IsOpposite( z1, z3 ) ) {
        ( connector2, connector3 ) = ( connector3, connector2 ) ;
      }
      else if ( IsOpposite( z1, z4 ) ) {
        ( connector2, connector4 ) = ( connector4, connector2 ) ;
      }
      else if ( false == IsOpposite( z1, z2 ) || false == IsOpposite( z3, z4 ) ) {
        return ( false, null ) ;
      }

      using var connectorTransaction = new SubTransaction( document ) ;
      try {
        connectorTransaction.Start() ;
        connector1.ConnectTo( connector2 ) ;
        connector1.ConnectTo( connector3 ) ;
        connector1.ConnectTo( connector4 ) ;
        connector2.ConnectTo( connector3 ) ;
        connector2.ConnectTo( connector4 ) ;
        connector3.ConnectTo( connector4 ) ;
        connectorTransaction.Commit() ;
      }
      catch {
        connectorTransaction.RollBack() ;
        return ( false, null ) ;
      }

      using var transaction = new SubTransaction( document ) ;
      try {
        transaction.Start() ;
        var family = document.Create.NewCrossFitting( connector1, connector2, connector3, connector4 ) ;
        if ( HasReverseConnectorDirection( family ) ) throw new Exception() ;
        transaction.Commit() ;

        return ( true, family ) ;
      }
      catch {
        transaction.RollBack() ;
        return ( false, null ) ;
      }
    }

    private static bool IsOpposite( XYZ dir1, XYZ dir2 )
    {
      return ( dir1.DotProduct( dir2 ) < -0.999 ) ;
    }

    /// <summary>
    /// Connect two connectors. Elbow is inserted if needed.
    /// </summary>
    /// <param name="connector1"></param>
    /// <param name="connector2"></param>
    private void ConnectTwoConnectors( Connector connector1, Connector connector2 )
    {
      var connectors = new[] { connector1, connector2 } ;
      var subRouteData = GuessSubRoute( connectors ) ;
      var fromEndPoints = GetNearestEnd( connectors, true ) ;
      var toEndPoints = GetNearestEnd( connectors, false ) ;

      var (success, fitting) = ConnectTwoConnectors( _document, connector1, connector2 ) ;
      if ( false == success ) {
        AddBadConnectorSet( connector1, connector2 ) ;
        return ;
      }
      if ( null == fitting ) return ;

      if ( null != subRouteData ) {
        MarkAsAutoRoutedElement( fitting, subRouteData.Value.RouteName, subRouteData.Value.SubRouteIndex, fromEndPoints, toEndPoints ) ;
      }

      SetRoutingFromToConnectorIdsForFitting( fitting ) ;
      EraseZeroLengthMEPCurves( fitting ) ;
    }

    /// <summary>
    /// Connect three connectors. Tee is inserted.
    /// </summary>
    /// <param name="connector1"></param>
    /// <param name="connector2"></param>
    /// <param name="connector3"></param>
    private void ConnectThreeConnectors( Connector connector1, Connector connector2, Connector connector3 )
    {
      var connectors = new[] { connector1, connector2, connector3 } ;
      var subRouteData = GuessSubRoute( connectors ) ;
      var fromEndPoints = GetNearestEnd( connectors, true ) ;
      var toEndPoints = GetNearestEnd( connectors, false ) ;

      var (success, fitting) = ConnectThreeConnectors( _document, connector1, connector2, connector3 ) ;
      if ( false == success ) {
        AddBadConnectorSet( connector1, connector2, connector3 ) ;
        return ;
      }

      if ( null == fitting ) return ;

      if ( null != subRouteData ) {
        MarkAsAutoRoutedElement( fitting, subRouteData.Value.RouteName, subRouteData.Value.SubRouteIndex, fromEndPoints, toEndPoints ) ;
      }

      SetRoutingFromToConnectorIdsForFitting( fitting ) ;
      EraseZeroLengthMEPCurves( fitting ) ;
    }

    /// <summary>
    /// Connect four connectors. Cross is inserted.
    /// </summary>
    /// <param name="connector1"></param>
    /// <param name="connector2"></param>
    /// <param name="connector3"></param>
    /// <param name="connector4"></param>
    private void ConnectFourConnectors( Connector connector1, Connector connector2, Connector connector3, Connector connector4 )
    {
      var connectors = new[] { connector1, connector2, connector3, connector4 } ;
      var subRouteData = GuessSubRoute( connectors ) ;
      var fromEndPoints = GetNearestEnd( connectors, true ) ;
      var toEndPoints = GetNearestEnd( connectors, false ) ;

      var (success, fitting) = ConnectFourConnectors( _document, connector1, connector2, connector3, connector4 ) ;
      if ( false == success ) {
        AddBadConnectorSet( connector1, connector2, connector3 ) ;
        return ;
      }

      if ( null == fitting ) return ;

      if ( null != subRouteData ) {
        MarkAsAutoRoutedElement( fitting, subRouteData.Value.RouteName, subRouteData.Value.SubRouteIndex, fromEndPoints, toEndPoints ) ;
      }

      SetRoutingFromToConnectorIdsForFitting( fitting ) ;
      EraseZeroLengthMEPCurves( fitting ) ;
    }

    private static (string RouteName, int SubRouteIndex)? GuessSubRoute( params Connector[] connectors )
    {
      var subRoutes = new List<(string RouteName, int SubRouteIndex)>() ;
      var counts = new Dictionary<(string RouteName, int SubRouteIndex), int>() ;
      foreach ( var connector in connectors ) {
        var elm = connector.Owner ;

        var routeName = elm.GetRouteName() ;
        if ( null == routeName ) continue ;

        var subRouteIndex = elm.GetSubRouteIndex() ;
        if ( null == subRouteIndex ) continue ;

        var tuple = ( routeName, subRouteIndex.Value ) ;
        subRoutes.Add( tuple ) ;

        if ( false == counts.TryGetValue( tuple, out var count ) ) {
          counts.Add( tuple, count ) ;
        }
        else {
          counts[ tuple ] = count + 1 ;
        }
      }

      if ( 0 == counts.Count ) return null ;

      var maxTuple = ( RouteName: string.Empty, SubRouteIndex: -1 ) ;
      var maxCount = -1 ;
      foreach ( var tuple in subRoutes ) {
        var count = counts[ tuple ] ;
        if ( count > maxCount || ( count == maxCount && IsGreaterThan( tuple, maxTuple ) ) ) {
          maxTuple = tuple ;
          maxCount = count ;
        }
      }

      return maxTuple ;
    }

    private static bool IsGreaterThan( (string RouteName, int SubRouteIndex) tuple1, (string RouteName, int SubRouteIndex) tuple2 )
    {
      if ( tuple1.RouteName != tuple2.RouteName ) return string.Compare( tuple1.RouteName, tuple2.RouteName, StringComparison.InvariantCulture ) > 0 ;
      return tuple1.SubRouteIndex > tuple2.SubRouteIndex ;
    }

    private static bool HasReverseConnectorDirection( FamilyInstance familyInstance )
    {
      // TODO
      return false ;
    }

    private static void EraseZeroLengthMEPCurves( FamilyInstance family )
    {
      foreach ( Connector connector in family.MEPModel.ConnectorManager.Connectors ) {
        EraseZeroLengthMEPCurves( connector ) ;
      }
    }

    private static void EraseZeroLengthMEPCurves( Connector connector )
    {
      var ductConn1 = GetConnectingMEPCurveConnector( connector ) ;
      if ( ductConn1?.Owner is not MEPCurve ) return ;

      var ductConn2 = GetAnotherMEPCurveConnector( ductConn1 ) ;
      if ( null == ductConn2 ) return ;

      if ( false == ductConn1.Origin.IsAlmostEqualTo( ductConn2.Origin ) ) return ;

      // TODO: erase duct
    }

    private static (Vector3d From, Vector3d To) GetLine( Connector connector )
    {
      var another = GetAnotherMEPCurveConnector( connector ) ?? throw new InvalidOperationException() ;
      return ( From: connector.Origin.To3dRaw(), To: another.Origin.To3dRaw() ) ;
    }

    private static Connector? GetAnotherMEPCurveConnector( Connector connector )
    {
      return connector.GetOtherConnectorsInOwner().UniqueOrDefault() ;
    }

    private static Connector? GetConnectingMEPCurveConnector( Connector connector )
    {
      return connector.GetConnectedConnectors().Where( c => c.Owner is MEPCurve ).UniqueOrDefault() ;
    }

    private void AddBadConnectorSet( params Connector[] connectors )
    {
      _badConnectors.Add( connectors ) ;
    }



    private static IReadOnlyCollection<IEndPoint> GetNearestEnd( Connector[] connectors, bool isFrom )
    {
      return connectors.SelectMany( c => c.Owner.GetNearestEndPoints( isFrom ) ).Distinct().EnumerateAll() ;
    }

    private static void MarkAsAutoRoutedElement( Element element, SubRoute subRoute, PassingEndPointInfo passingEndPointInfo )
    {
      MarkAsAutoRoutedElement( element, subRoute.Route.RouteName, subRoute.SubRouteIndex, passingEndPointInfo.FromEndPoints, passingEndPointInfo.ToEndPoints );
    }

    private static void MarkAsAutoRoutedElement( Element element, string routeId, int subRouteIndex, IEnumerable<IEndPoint> nearestFrom, IEnumerable<IEndPoint> nearestTo )
    {
      element.SetProperty( RoutingParameter.RouteName, routeId ) ;
      element.SetProperty( RoutingParameter.SubRouteIndex, subRouteIndex ) ;
      element.SetProperty( RoutingParameter.NearestFromSideEndPoints, nearestFrom.Stringify() );
      element.SetProperty( RoutingParameter.NearestToSideEndPoints, nearestTo.Stringify() );
    }

    private static void SetRoutingFromToConnectorIdsForFitting( Element element )
    {
      var connectorSet = element.GetConnectors().Select( c => new ConnectorId( c ) ).ToHashSet() ;

      var fromList = new List<int>() ;
      var toList = new List<int>() ;
      element.GetConnectors().ForEach( conn =>
      {
        if ( false == conn.IsAnyEnd() ) return ;

        foreach ( var partner in conn.GetConnectedConnectors().Where( c => connectorSet.Contains( new ConnectorId( c ) ) ) ) {
          if ( null == partner ) return ;
          if ( partner.IsRoutingConnector( true ) ) {
            toList.Add( conn.Id ) ;
            return ;
          }
          if ( partner.IsRoutingConnector( false ) ) {
            fromList.Add( conn.Id ) ;
            return ;
          }
        }
      } ) ;

      element.SetRoutedElementFromToConnectorIds( fromList, toList ) ;
    }
  }
}