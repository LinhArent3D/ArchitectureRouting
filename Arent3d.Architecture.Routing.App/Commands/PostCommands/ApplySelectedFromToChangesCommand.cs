﻿using System.Collections.Generic ;
using System.ComponentModel ;
using Arent3d.Architecture.Routing.App.ViewModel ;
using Arent3d.Revit ;
using Arent3d.Utility ;
using Autodesk.Revit.Attributes ;
using Autodesk.Revit.UI ;


namespace Arent3d.Architecture.Routing.App.Commands.PostCommands
{
  [RevitAddin( Guid )]
  [DisplayName( "Apply Selected From-To Changes" )]
  [Transaction( TransactionMode.Manual )]
  public class ApplySelectedFromToChangesCommand : Routing.RoutingCommandBase
  {
    private const string Guid = "D1464970-1251-442F-8754-E59E293FBC9D" ;
    protected override string GetTransactionNameKey() => "TransactionName.Commands.PostCommands.ApplySelectedFromToChangesCommand" ;

    protected override IAsyncEnumerable<(string RouteName, RouteSegment Segment)>? GetRouteSegmentsParallelToTransaction( UIDocument uiDocument )
    {
      if ( SelectedFromToViewModel.PropertySourceType is { } propertySource ) {
        var route = propertySource.TargetRoute ;
        var subRoutes = propertySource.TargetSubRoutes ;
        var diameters = propertySource.Diameters ;
        var systemTypes = propertySource.SystemTypes ;
        var curveTypes = propertySource.CurveTypes ;

        if ( diameters != null && systemTypes != null && curveTypes != null ) {
          if ( route != null && subRoutes != null ) {
            //Change SystemType
            route.SetMEPSystemType( systemTypes[ SelectedFromToViewModel.SelectedSystemTypeIndex ] ) ;

            foreach ( var subRoute in subRoutes ) {
              //Change Diameter
              if ( SelectedFromToViewModel.SelectedDiameterIndex != -1 ) {
                subRoute.ChangePreferredNominalDiameter( diameters[ SelectedFromToViewModel.SelectedDiameterIndex ] ) ;
              }

              //Change CurveType
              if ( SelectedFromToViewModel.SelectedCurveTypeIndex != -1 ) {
                subRoute.SetMEPCurveType( curveTypes[ SelectedFromToViewModel.SelectedCurveTypeIndex ] ) ;
              }

              //ChangeDirect
              if ( SelectedFromToViewModel.IsDirect is { } isDirect ) {
                subRoute.ChangeIsRoutingOnPipeSpace( isDirect ) ;
              }
            }

            return route.CollectAllDescendantBranches().ToSegmentsWithName().EnumerateAll().ToAsyncEnumerable() ;
          }
        }
      }

      return base.GetRouteSegmentsParallelToTransaction( uiDocument ) ;
    }
  }
}