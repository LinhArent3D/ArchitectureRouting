﻿using System ;
using System.Collections ;
using System.Collections.Generic ;
using System.Linq ;
using Arent3d.Architecture.Routing.App.Forms ;
using Arent3d.Revit.UI ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.UI ;

namespace Arent3d.Architecture.Routing.App.ViewModel
{
  static class SelectedFromToViewModel
  {
    private static UIDocument? UiDoc { get ; set ; }

    //Route
    public static Route? TargetRoute { get ; set ; }

    //Diameter
    public static int SelectedDiameterIndex { get ; private set ; }

    //SystemType 
    public static int SelectedSystemTypeIndex { get ; private set ; }

    //CurveType
    public static int SelectedCurveTypeIndex { get ; private set ; }

    //Direct
    public static bool? IsDirect { get ; set ; }
    
    //FixedHeight
    public static bool OnHeightSetting { get ; set ; }
    public static double FixedHeight { get ; private set ; }

    public static PropertySource.RoutePropertySource? PropertySourceType { get ; private set ; }

    //Direct
    public static FromToItem? FromToItem { get; set; }


    static SelectedFromToViewModel()
    {
    }

    /// <summary>
    /// Set Selected Fromt-To Info
    /// </summary>
    /// <param name="uiDoc"></param>
    /// <param name="doc"></param>
    /// <param name="subRoutes"></param>
    /// <param name="fromToItem"></param>
    public static void SetSelectedFromToInfo( UIDocument uiDoc, Document doc, IReadOnlyCollection<SubRoute>? subRoutes, FromToItem fromToItem )
    {
      UiDoc = uiDoc ;
      TargetRoute = subRoutes?.ElementAt( 0 ).Route ;

      if ( fromToItem.PropertySourceType is PropertySource.RoutePropertySource routePropertySource ) {
        PropertySourceType = routePropertySource ;
      }

      FromToItem = fromToItem;
    }


    /// <summary>
    /// Set Dilaog Parameters and send PostCommand
    /// </summary>
    /// <param name="selectedDiameter"></param>
    /// <param name="selectedSystemType"></param>
    /// <param name="selectedDirect"></param>
    /// <returns></returns>
    public static bool ApplySelectedChanges( int selectedDiameter, int selectedSystemType, int selectedCurveType, bool? selectedDirect, bool? heightSetting, double fixedHeight )
    {
      if ( UiDoc != null ) {
        SelectedDiameterIndex = selectedDiameter ;
        SelectedSystemTypeIndex = selectedSystemType ;
        SelectedCurveTypeIndex = selectedCurveType ;
        IsDirect = selectedDirect ;
        if ( heightSetting is { } onHeightSetting) {
          OnHeightSetting = onHeightSetting ;
        }
        FixedHeight = GetTotalHeight(fixedHeight) ;
        UiDoc.Application.PostCommand<Commands.PostCommands.ApplySelectedFromToChangesCommand>() ;
        return true ;
      }
      else {
        return false ;
      }
    }

    /// <summary>
    /// Reset Diameter List by Curve Type
    /// </summary>
    /// <param name="curveTypeIndex"></param>
    /// <returns></returns>
    public static IEnumerable<double> ResetNominalDiameters( int curveTypeIndex )
    {
      if ( PropertySourceType?.CurveTypes is not { } curveTypes|| null == UiDoc ) return Enumerable.Empty<double>() ;
      if ( curveTypeIndex < 0 || curveTypes.Count <= curveTypeIndex ) return Enumerable.Empty<double>() ;

      return curveTypes[ curveTypeIndex ].GetNominalDiameters( UiDoc.Document.Application.VertexTolerance ) ;
    }

    private static double GetTotalHeight( double selectedHeight )
    {
      var targetHeight = 0.0 ;
      var connector = TargetRoute?.FirstFromConnector()?.GetConnector()?.Owner ;
      var level = connector?.Document.GetElement(connector.LevelId) as Level;
      var floorHeight = level?.Elevation ;
      if ( floorHeight != null && TargetRoute?.GetSubRoute(0)?.GetDiameter() is {} diameter) {
        targetHeight = UnitUtils.ConvertToInternalUnits(selectedHeight, UnitTypeId.Millimeters  )  + (double)floorHeight - diameter/2;
        var test = UnitUtils.ConvertFromInternalUnits( targetHeight, UnitTypeId.Millimeters ) ;
      }

      return targetHeight ;
    }
  }
}