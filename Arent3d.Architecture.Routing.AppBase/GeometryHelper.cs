﻿using System ;
using System.Collections.Generic ;
using System.Linq ;
using Arent3d.Revit ;
using Arent3d.Revit.I18n ;
using Arent3d.Utility ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.DB.Electrical ;

namespace Arent3d.Architecture.Routing.AppBase
{
  public static class GeometryHelper
  {
    public const double Tolerance = 0.0001 ;

    private static IList<Element> IntersectElements( this (XYZ Elbow, XYZ End) leader, Document document, BuiltInCategory builtInCategory )
    {
      var (minHeight, maxHeight) = GetHeightRange( document ) ;
      if ( minHeight.Equals( maxHeight ) )
        return new List<Element>() ;

      var minOutline = new XYZ( Math.Min( leader.Elbow.X, leader.End.X ), Math.Min( leader.Elbow.Y, leader.End.Y ), minHeight ) ;
      var maxOutline = new XYZ( Math.Max( leader.Elbow.X, leader.End.X ), Math.Max( leader.Elbow.Y, leader.End.Y ), maxHeight ) ;

      var intersectFilter = new BoundingBoxIntersectsFilter( new Outline( minOutline, maxOutline ) ) ;
      var categoryFilter = new ElementCategoryFilter( builtInCategory ) ;
      var logicalAndFilter = new LogicalAndFilter( intersectFilter, categoryFilter ) ;
      var elements = new FilteredElementCollector( document, document.ActiveView.Id ).WherePasses( logicalAndFilter ).ToElements().ToList() ;

      return elements ;
    }

    public static List<Curve> IntersectCurveLeader( Document document, (XYZ Elbow, XYZ End) leader )
    {
      var curvesIntersected = new List<Curve> { Line.CreateBound( leader.Elbow, leader.End ) } ;
      var (minHeight, maxHeight) = GetHeightRange( document ) ;

      var conduits = leader.IntersectElements( document, BuiltInCategory.OST_Conduit ) ;
      var conduitFilters = new List<Element>() ;
      foreach ( var conduit in conduits ) {
        if ( conduit.Location is not LocationCurve { Curve: Line locationLine } )
          continue ;

        if ( Math.Abs( locationLine.Direction.DotProduct( document.ActiveView.ViewDirection ) ) < 0.0001 ) {
          conduitFilters.Add( conduit ) ;
        }
      }

      var solidOption = new SolidCurveIntersectionOptions { ResultType = SolidCurveIntersectionMode.CurveSegmentsOutside } ;
      foreach ( var conduitFilter in conduitFilters ) {
        if ( conduitFilter.Location is not LocationCurve { Curve: Line locationLine } ) continue ;
        var (startPoint, endPoint) = ( locationLine.GetEndPoint( 0 ), locationLine.GetEndPoint( 1 ) ) ;
        var line = Line.CreateBound( new XYZ( startPoint.X, startPoint.Y, leader.Elbow.Z ), new XYZ( endPoint.X, endPoint.Y, leader.Elbow.Z ) ) ;

        var diameter = conduitFilter.ParametersMap.get_Item( "Revit.Property.Builtin.OutsideDiameter".GetDocumentStringByKeyOrDefault( document, "Outside Diameter" ) ).AsDouble() ;
        var curveLoop = CurveLoop.CreateViaThicken( line.Clone(), 9 * diameter, document.ActiveView.ViewDirection ) ;
        var transform = Transform.CreateTranslation( document.ActiveView.ViewDirection.Negate() * ( leader.Elbow.Z - minHeight ) ) ;
        curveLoop = CurveLoop.CreateViaTransform( curveLoop, transform ) ;

        curvesIntersected = GetCurvesOutsiteSolid( document, curveLoop, maxHeight - minHeight, solidOption, curvesIntersected ) ;
      }

      var conduitFittings = leader.IntersectElements( document, BuiltInCategory.OST_ConduitFitting ) ;
      foreach ( var conduitFitting in conduitFittings ) {
        var curveLoop = GetBoundaryBoundingBox( conduitFitting.get_BoundingBox( null ), minHeight ) ;
        curveLoop = CurveLoop.CreateViaOffset( curveLoop, 50d.MillimetersToRevitUnits(), document.ActiveView.ViewDirection ) ;
        curvesIntersected = GetCurvesOutsiteSolid( document, curveLoop, maxHeight - minHeight, solidOption, curvesIntersected ) ;
      }

      return curvesIntersected ;
    }

    private static List<Curve> GetCurvesOutsiteSolid( Document document, CurveLoop curveLoopBase, double heightSolid, SolidCurveIntersectionOptions solidOption, List<Curve> curvesIntersected )
    {
      var solid = GeometryCreationUtilities.CreateExtrusionGeometry( new List<CurveLoop> { curveLoopBase }, document.ActiveView.ViewDirection, heightSolid ) ;
      var curvesIntersectSolid = new List<Curve>() ;

      foreach ( var results in from curveIntersected in curvesIntersected select solid.IntersectWithCurve( curveIntersected, solidOption ) ) {
        results?.ForEach( curve =>
        {
          if ( curve.Length > document.Application.ShortCurveTolerance ) {
            curvesIntersectSolid.Add( curve ) ;
          }
        } ) ;
      }

      return curvesIntersectSolid ;
    }

    private static (double, double) GetHeightRange( Document document )
    {
      var levels = document.GetAllElements<Level>() ;
      if ( ! levels.Any() )
        return ( 0d, 0d ) ;

      var elevations = levels.Select( x => x.Elevation ).ToList() ;

      return ( elevations.Min(), elevations.Max() ) ;
    }

    public static (DetailCurve? detailCurve, int? endPoint) GetCurveClosestPoint( IList<DetailCurve>? detailCurves, XYZ point )
    {
      if ( ! detailCurves?.Any() ?? true )
        return ( null, null ) ;

      var lists = new List<(DetailCurve detailCurve, (double distance, int endPoint) point)>() ;

      foreach ( var detailCurve in detailCurves! ) {
        var dis1 = detailCurve.GeometryCurve.GetEndPoint( 0 ).DistanceTo( point ) ;
        var dis2 = detailCurve.GeometryCurve.GetEndPoint( 1 ).DistanceTo( point ) ;

        lists.Add( dis1 < dis2 ? ( detailCurve, ( dis1, 0 ) ) : ( detailCurve, ( dis2, 1 ) ) ) ;
      }

      var min = lists.MinBy( x => x.point.distance ) ;
      return ( min.detailCurve, min.point.endPoint ) ;
    }

    public static Line CreateUnderLineText( TextNote textNote, XYZ basePoint )
    {
      var height = ( textNote.Height + textNote.TextNoteType.get_Parameter( BuiltInParameter.LEADER_OFFSET_SHEET ).AsDouble() ) * textNote.Document.ActiveView.Scale ;
      var coord = Transform.CreateTranslation( textNote.UpDirection.Negate() * height ).OfPoint( textNote.Coord ) ;
      var width = ( textNote.HorizontalAlignment == HorizontalTextAlignment.Right ? -1 : 1 ) * textNote.Width * textNote.Document.ActiveView.Scale / 2 ;
      var middle = Transform.CreateTranslation( textNote.BaseDirection * width ).OfPoint( coord ) ;

      return Line.CreateBound( new XYZ( coord.X, coord.Y, basePoint.Z ), new XYZ( middle.X, middle.Y, basePoint.Z ) ) ;
    }

    public static CurveLoop GetOutlineTextNote( TextNote textNote )
    {
      var offset = textNote.TextNoteType.get_Parameter( BuiltInParameter.LEADER_OFFSET_SHEET ).AsDouble() ;
      var height = ( textNote.Height + 2 * offset ) * textNote.Document.ActiveView.Scale ;
      var width = ( textNote.HorizontalAlignment == HorizontalTextAlignment.Right ? -1 : 1 ) * ( textNote.Width + 2 * offset ) * textNote.Document.ActiveView.Scale ;

      var transformHeight = Transform.CreateTranslation( textNote.UpDirection.Negate() * height ) ;
      var transformWidth = Transform.CreateTranslation( textNote.BaseDirection * width ) ;
      var transformCoord = Transform.CreateTranslation( textNote.UpDirection.Add( textNote.HorizontalAlignment == HorizontalTextAlignment.Right ? textNote.BaseDirection : textNote.BaseDirection.Negate() ) * offset * textNote.Document.ActiveView.Scale ) ;

      var curveLoop = new CurveLoop() ;
      var p1 = transformCoord.OfPoint( textNote.Coord ) ;
      var p2 = transformWidth.OfPoint( p1 ) ;
      var p3 = transformHeight.OfPoint( p2 ) ;
      var p4 = transformHeight.OfPoint( p1 ) ;

      curveLoop.Append( Line.CreateBound( p1, p2 ) ) ;
      curveLoop.Append( Line.CreateBound( p2, p3 ) ) ;
      curveLoop.Append( Line.CreateBound( p3, p4 ) ) ;
      curveLoop.Append( Line.CreateBound( p4, p1 ) ) ;

      return curveLoop ;
    }

    public static List<DetailCurve> CreateDetailCurve( View? view, List<Curve> curves )
    {
      var detailCurves = new List<DetailCurve>() ;
      if ( null == view )
        return detailCurves ;

      var graphicsStyle = view.Document.Settings.Categories.get_Item( BuiltInCategory.OST_CurvesMediumLines ).GetGraphicsStyle( GraphicsStyleType.Projection ) ;

      foreach ( var curve in curves ) {
        var detailCurve = view.Document.Create.NewDetailCurve( view, curve ) ;
        detailCurve.LineStyle = graphicsStyle ;
        detailCurves.Add( detailCurve ) ;
      }

      return detailCurves ;
    }

    public static CurveLoop GetBoundaryBoundingBox( BoundingBoxXYZ boundingBox, double elevation )
    {
      var curveLoop = new CurveLoop() ;

      var p1 = new XYZ( boundingBox.Min.X, boundingBox.Min.Y, elevation ) ;
      var p2 = new XYZ( boundingBox.Max.X, boundingBox.Min.Y, elevation ) ;
      var p3 = new XYZ( boundingBox.Max.X, boundingBox.Max.Y, elevation ) ;
      var p4 = new XYZ( boundingBox.Min.X, boundingBox.Max.Y, elevation ) ;

      curveLoop.Append( Line.CreateBound( p1, p2 ) ) ;
      curveLoop.Append( Line.CreateBound( p2, p3 ) ) ;
      curveLoop.Append( Line.CreateBound( p3, p4 ) ) ;
      curveLoop.Append( Line.CreateBound( p4, p1 ) ) ;

      return curveLoop ;
    }

    public static bool IsCurveIntersectCurves( Curve hostCurve, List<Curve> curves )
    {
      if ( ! curves.Any() )
        return false ;

      foreach ( var curve in curves ) {
        hostCurve.Intersect( curve, out var resultArray ) ;
        if ( null != resultArray && resultArray.Cast<IntersectionResult>().Any( result => null != result.XYZPoint ) ) {
          return true ;
        }
      }

      return false ;
    }
  }
}