﻿using Autodesk.Revit.DB ;

namespace Arent3d.Architecture.Routing.AppBase.Utils
{
  internal static class XyzUtil
    {
      public static bool IsIntersect( XYZ positionFrom, XYZ positionTo, XYZ positionFromNext, XYZ powerToNext )
      {
        if ( ThreePointOrientation( positionFromNext, powerToNext, positionFrom ) != ThreePointOrientation( positionFromNext, powerToNext, positionTo ) &&
             ThreePointOrientation( positionFrom, positionTo, positionFromNext ) != ThreePointOrientation( positionFrom, positionTo, powerToNext ) ) return true ;
        return false ;
      }

      public static  string ThreePointOrientation( XYZ a, XYZ b, XYZ c )
      {
        double check = ( b.Y - a.Y ) * ( c.X - b.X ) - ( c.Y - b.Y ) * ( b.X - a.X ) ;
        return check > 0 ? "clockwise" : "counterclockwise" ;
      }
      
      public static XYZ GetMiddlePoint( XYZ fromPoint, XYZ toPoint, XYZ direction )
      {
        if(direction.Y is 1 or -1) return new XYZ( ( fromPoint.X + toPoint.X ) / 2 - 1.5 , ( fromPoint.Y + toPoint.Y ) / 2, fromPoint.Z ) ;
      
        if(direction.X is 1 or -1) return new XYZ( ( fromPoint.X + toPoint.X ) / 2, ( fromPoint.Y + toPoint.Y ) / 2 + 1.5, fromPoint.Z ) ;
      
        return new XYZ( ( fromPoint.X + toPoint.X ) / 2, ( fromPoint.Y + toPoint.Y ) / 2, fromPoint.Z ) ;
      }
    }
}