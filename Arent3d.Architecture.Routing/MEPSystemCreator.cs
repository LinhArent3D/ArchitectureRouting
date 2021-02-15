using System ;
using System.Collections.Generic ;
using System.Linq ;
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
    private readonly RouteVertexToConnectorMapper _connectorMapper = new() ;
    
    /// <summary>
    /// Returns pass-point-to-connector relation manager.
    /// </summary>
    public PassPointConnectorMapper PassPointConnectorMapper { get ; }= new() ;

    private readonly Level _level ;

    private readonly MEPSystemType _systemType ;
    private readonly MEPSystem? _system ;
    private readonly MEPCurveType _curveType ;

    private readonly List<Connector> _badConnectors = new() ;

    public MEPSystemCreator( Document document, AutoRoutingTarget autoRoutingTarget, RouteMEPSystem routeMepSystem )
    {
      _document = document ;
      _autoRoutingTarget = autoRoutingTarget ;
      _level = GetLevel( document, autoRoutingTarget ) ;
      _systemType = routeMepSystem.MEPSystemType ;
      _system = routeMepSystem.MEPSystem ;
      _curveType = routeMepSystem.CurveType ;
    }

    private static Level GetLevel( Document document, AutoRoutingTarget autoRoutingTarget )
    {
      var level = autoRoutingTarget.EndPoints.Select( e => document.GetElementById<Level>( e.ReferenceConnector.Owner.LevelId ) ).FirstOrDefault( l => l != null && l.IsValidObject ) ;
      return level ?? Level.Create( document, 0.0 ) ;
    }

    public IReadOnlyCollection<Connector> GetBadConnectors() => _badConnectors ;

    /// <summary>
    /// Registers related end route vertex and connector for an end point.
    /// </summary>
    /// <param name="routeVertex">A route vertex generated from an end point.</param>
    /// <exception cref="ArgumentException"><see cref="routeVertex"/> is not generated from an end point.</exception>
    public void RegisterEndPointConnector( IRouteVertex routeVertex )
    {
      if ( GetEndPoint( routeVertex.LineInfo ) is ConnectorEndPoint cep ) {
        _connectorMapper.Add( routeVertex, cep.RoutingConnector ) ;
      }
    }

    /// <summary>
    /// Registers related end route vertex and connector for an end point.
    /// </summary>
    /// <param name="routeVertex">A route vertex generated from an end point.</param>
    /// <param name="connector">A Connector generated by mep creation.</param>
    /// <exception cref="InvalidOperationException"><see cref="routeVertex"/> is not generated from an end point.</exception>
    private void RegisterPassPoint( IRouteVertex routeVertex, Connector connector )
    {
      if ( routeVertex is not TerminalPoint ) return ;

      if ( GetEndPoint( routeVertex.LineInfo ) is PassPointEndPoint ep ) {
        PassPointConnectorMapper.Add( ep.Element.Id, ep.SideType, connector ) ;
      }
    }

    private static EndPoint GetEndPoint( IAutoRoutingEndPoint endPoint )
    {
      return endPoint switch
      {
        EndPoint ep => ep,
        IPseudoEndPoint pep => GetEndPoint( pep.Source ),
        _ => throw new ArgumentException()
      } ;
    }

    /// <summary>
    /// Creates a duct from a route edge.
    /// </summary>
    /// <param name="routeEdge">A route edge.</param>
    /// <returns>Newly created duct.</returns>
    public Element CreateEdgeElement( IRouteEdge routeEdge )
    {
      var startPos = _connectorMapper.GetNewConnectorPosition( routeEdge.Start, routeEdge.End ).ToXYZ() ;
      var endPos = _connectorMapper.GetNewConnectorPosition( routeEdge.End, routeEdge.Start ).ToXYZ() ;

      var baseConnector = ( routeEdge.LineInfo as EndPoint )?.ReferenceConnector ;
      if ( null == baseConnector ) throw new InvalidOperationException() ;

      var element = baseConnector.Domain switch
      {
        Domain.DomainHvac => CreateDuct( startPos, endPos, ( _curveType as DuctType )! ),
        Domain.DomainPiping => CreatePipe( startPos, endPos, ( _curveType as PipeType )! ),
        Domain.DomainCableTrayConduit => CreateCableTray( startPos, endPos, ( _curveType as CableTrayType )! ),
        Domain.DomainElectrical => throw new InvalidOperationException(), // TODO
        _ => throw new InvalidOperationException(),
      } ;

      MarkAsAutoRoutedElement( element, _autoRoutingTarget.SubRoute ) ;

      var manager = element.GetConnectorManager() ?? throw new InvalidOperationException() ;

      var startConnector = GetConnector( manager, startPos ) ;
      var endConnector = GetConnector( manager, endPos ) ;
      startConnector.SetDiameter( routeEdge.Start.PipeDiameter ) ;
      endConnector.SetDiameter( routeEdge.End.PipeDiameter ) ;

      element.SetRoutedElementFromToConnectorIds( new[] { startConnector.Id }, new[] { endConnector.Id } ) ;

      RegisterPassPoint( routeEdge.Start, startConnector ) ;
      RegisterPassPoint( routeEdge.End, endConnector ) ;

      _connectorMapper.Add( routeEdge.Start, startConnector ) ;
      _connectorMapper.Add( routeEdge.End, endConnector ) ;

      return element ;
    }

    private MEPCurve CreateDuct( XYZ startPos, XYZ endPos, DuctType ductType )
    {
      var duct = Duct.Create( _document, _systemType.Id, ductType.Id, _level.Id, startPos, endPos ) ;
      if ( null != _system ) {
        duct.SetSystemType( _system.Id ) ;
      }
      return duct ;
    }
    private MEPCurve CreatePipe( XYZ startPos, XYZ endPos, PipeType pipeType )
    {
      var pipe = Pipe.Create( _document, _systemType.Id, pipeType.Id, _level.Id, startPos, endPos ) ;
      if ( null != _system ) {
        pipe.SetSystemType( _system.Id ) ;
      }
      return pipe ;
    }
    private MEPCurve CreateCableTray( XYZ startPos, XYZ endPos, CableTrayType cableTrayType )
    {
      return CableTray.Create( _document, cableTrayType.Id, startPos, endPos, _level.Id ) ;
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
      foreach ( var connectors in _connectorMapper ) {
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
    /// Connect two connectors. Elbow is inserted if needed.
    /// </summary>
    /// <param name="connector1"></param>
    /// <param name="connector2"></param>
    private void ConnectTwoConnectors( Connector connector1, Connector connector2 )
    {
      using var connectorTransaction = new SubTransaction( _document ) ;
      try {
        connectorTransaction.Start() ;
        connector1.ConnectTo( connector2 ) ;
        connectorTransaction.Commit() ;
      }
      catch {
        connectorTransaction.RollBack() ;
        AddBadConnector( connector1 ) ;
        return ;
      }

      var dir1 = connector1.CoordinateSystem.BasisZ.To3d() ;
      var dir2 = connector2.CoordinateSystem.BasisZ.To3d() ;

      if ( 0.9 < Math.Abs( Vector3d.Dot( dir1, dir2 ) ) ) {
        // Connect directly(-1) or bad connection(+1)
      }
      else {
        // Orthogonal
        using var transaction = new SubTransaction( _document ) ;
        try {
          transaction.Start() ;
          var family = _document.Create.NewElbowFitting( connector1, connector2 ) ;
          if ( HasReverseConnectorDirection( family ) ) throw new Exception() ;
          MarkAsAutoRoutedElement( family, connector1, connector2 ) ;
          SetRoutingFromToConnectorIdsForFitting( family, connector1, connector2 ) ;
          EraseZeroLengthMEPCurves( family ) ;
          transaction.Commit() ;
        }
        catch {
          transaction.RollBack() ;
          AddBadConnector( connector1 ) ;
        }
      }
    }

    /// <summary>
    /// Connect three connectors. Tee is inserted.
    /// </summary>
    /// <param name="connector1"></param>
    /// <param name="connector2"></param>
    /// <param name="connector3"></param>
    private void ConnectThreeConnectors( Connector connector1, Connector connector2, Connector connector3 )
    {
      using var connectorTransaction = new SubTransaction( _document ) ;
      try {
        connectorTransaction.Start() ;
        connector1.ConnectTo( connector2 ) ;
        connector1.ConnectTo( connector3 ) ;
        connector2.ConnectTo( connector3 ) ;
        connectorTransaction.Commit() ;
      }
      catch {
        connectorTransaction.RollBack() ;
        AddBadConnector( connector1 ) ;
        return ;
      }

      using var transaction = new SubTransaction( _document ) ;
      try {
        transaction.Start() ;
        var family = _document.Create.NewTeeFitting( connector1, connector2, connector3 ) ;
        if ( HasReverseConnectorDirection( family ) ) throw new Exception() ;
        MarkAsAutoRoutedElement( family, connector1, connector2, connector3 ) ;
        SetRoutingFromToConnectorIdsForFitting( family, connector1, connector2, connector3 ) ;
        EraseZeroLengthMEPCurves( family ) ;
        transaction.Commit() ;
      }
      catch {
        transaction.RollBack() ;
        AddBadConnector( connector1 ) ;
      }
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
      using var connectorTransaction = new SubTransaction( _document ) ;
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
        AddBadConnector( connector1 ) ;
        return ;
      }

      using var transaction = new SubTransaction( _document ) ;
      try {
        transaction.Start() ;
        var family = _document.Create.NewCrossFitting( connector1, connector2, connector3, connector4 ) ;
        if ( HasReverseConnectorDirection( family ) ) throw new Exception() ;
        MarkAsAutoRoutedElement( family, connector1, connector2, connector3, connector4 ) ;
        SetRoutingFromToConnectorIdsForFitting( family, connector1, connector2, connector3, connector4 ) ;
        EraseZeroLengthMEPCurves( family ) ;
        transaction.Commit() ;
      }
      catch {
        transaction.RollBack() ;
        AddBadConnector( connector1 ) ;
      }
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
      return ( From: connector.Origin.To3d(), To: another.Origin.To3d() ) ;
    }

    private static Connector? GetAnotherMEPCurveConnector( Connector connector )
    {
      return connector.GetOtherConnectorsInOwner().UniqueOrDefault() ;
    }

    private static Connector? GetConnectingMEPCurveConnector( Connector connector )
    {
      return connector.GetConnectedConnectors().Where( c => c.Owner is MEPCurve ).UniqueOrDefault() ;
    }

    private void AddBadConnector( Connector connector )
    {
      _badConnectors.Add( connector ) ;
    }



    private void MarkAsAutoRoutedElement( Element element, SubRoute subRoute )
    {
      MarkAsAutoRoutedElement( element, subRoute.Route.RouteId, subRoute.SubRouteIndex );
    }
    private void MarkAsAutoRoutedElement( Element element, params Connector[] connectors )
    {
      var (routeName, subRouteIndex) = connectors.Select( GetRouteNameAndSubRouteIndex ).FirstOrDefault( tuple => null == tuple.RouteName ) ;
      MarkAsAutoRoutedElement( element, routeName ?? string.Empty, subRouteIndex ) ;
    }
    private static void MarkAsAutoRoutedElement( Element element, string routeId, int subRouteIndex )
    {
      element.SetProperty( RoutingParameter.RouteName, routeId ) ;
      element.SetProperty( RoutingParameter.SubRouteIndex, subRouteIndex ) ;
    }

    private static (string? RouteName, int SubRouteIndex) GetRouteNameAndSubRouteIndex( Connector connector )
    {
      var routeName = connector.Owner.GetRouteName() ;
      if ( null == routeName ) return ( null, 0 ) ;

      var subRouteIndex = connector.Owner.GetSubRouteIndex() ;
      if ( null == subRouteIndex ) return ( null, 0 ) ;

      return ( RouteName: routeName, SubRouteIndex: subRouteIndex.Value ) ;
    }

    private static void SetRoutingFromToConnectorIdsForFitting( Element element, params Connector[] connectors )
    {
      var manager = element.GetConnectorManager() ;
      if ( null == manager ) throw new InvalidOperationException() ;

      var connectorSet = connectors.Select( c => c.GetIndicator() ).ToHashSet() ;
      var fromList = new List<int>() ;
      var toList = new List<int>() ;
      manager.Connectors.OfType<Connector>().ForEach( conn =>
      {
        if ( false == conn.IsAnyEnd() ) return ;

        foreach ( var partner in conn.GetLogicallyConnectedConnectors().Where( c => connectorSet.Contains( c.GetIndicator() ) ) ) {
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



    #region Eraseing previous data

    /// <summary>
    /// Erase all previous ducts and pipes in between routing targets.
    /// </summary>
    /// <param name="targets">Routing targets.</param>
    public static void ErasePreviousRoutes( IReadOnlyCollection<AutoRoutingTarget> targets )
    {
      var connectors = targets.SelectMany( x => x.EndPoints ).Select( ep => ep.ReferenceConnector ).EnumerateAll() ;
      if ( 0 == connectors.Count ) return ;

      var document = connectors.First().Owner.Document ;
      var erasingRouteNames = targets.Select( t => t.SubRoute.Route.RouteId ).ToHashSet() ;

      ErasePreviousRoutes( document, connectors, erasingRouteNames ) ;
    }

    public static void ErasePreviousRoutes( Document document, IReadOnlyCollection<Connector> endConnectors, HashSet<string> erasingRouteNames )
    {
      var routeElements = CollectRouteElementIds( erasingRouteNames, endConnectors ) ;

      if ( 0 != routeElements.Count ) {
        document!.Delete( routeElements ) ;
      }
    }

    /// <summary>
    /// Collect elements in the target routes joined to connectors.
    /// </summary>
    /// <param name="targetRouteNames">Target routes.</param>
    /// <param name="connectors">Connectors.</param>
    /// <returns></returns>
    public static ICollection<ElementId> CollectRouteElementIds( HashSet<string> targetRouteNames, IReadOnlyCollection<Connector> connectors )
    {
      var endConnectorChecker = new EndConnectorChecker( connectors ) ;

      var stack = new Stack<Connector>( connectors.Where( c => c.IsConnected ) ) ;
      var eraseTargets = new HashSet<ElementId>() ;
      while ( 0 != stack.Count ) {
        var connector = stack.Pop() ;
        var otherConnectors = connector.GetLogicallyConnectedConnectors().OfEnd().EnumerateAll() ;

        foreach ( var nextConnector in otherConnectors.OfEnd().Where( c => ! endConnectorChecker.IsEnd( c ) ).EnumerateAll() ) {
          var owner = nextConnector.Owner ;

          if ( owner.GetRouteName() is { } routeName && ! targetRouteNames.Contains( routeName ) ) continue ;

          if ( false == AddEraseTargets( eraseTargets, owner ) ) continue ;

          // add into the lookup stack.
          nextConnector.GetOtherConnectorsInOwner().OfEnd().Where( c => c.IsConnected ).ForEach( stack.Push ) ;
        }
      }

      return eraseTargets ;
    }

    private static bool AddEraseTargets( HashSet<ElementId> eraseTargets, Element owner )
    {
      if ( ! eraseTargets.Add( owner.Id ) ) return false ;

      foreach ( var c in owner.GetConnectorManager()!.Connectors.OfType<Connector>().SelectMany( RoutingElementExtensions.GetConnectedConnectors ) ) {
        if ( false == c.IsAnyEnd() ) continue ;
        if ( false == c.Owner.IsFittingElement() ) continue ;

        AddEraseTargets( eraseTargets, c.Owner ) ;
      }

      return true ;
    }

    private class EndConnectorChecker
    {
      private readonly HashSet<ConnectorIndicator> _endConnectorIds ;
      
      public EndConnectorChecker( IEnumerable<Connector> connectors )
      {
        _endConnectorIds = connectors.Select( RoutingElementExtensions.GetIndicator ).ToHashSet() ;
      }

      public bool IsEnd( Connector connector )
      {
        if ( _endConnectorIds.Contains( connector.GetIndicator() ) ) return true ;

        if ( null == connector.Owner ) return true ;
        if ( false == connector.Owner.IsAutoRoutingGeneratedElement() ) return true ;

        return false ;
      }
    }    

    #endregion
  }
}