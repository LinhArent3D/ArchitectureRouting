// #define TTE_BUILD // 高砂熱学用メニュー

using Arent3d.Architecture.Routing.Mechanical.App.Commands ;
using Arent3d.Architecture.Routing.Mechanical.App.Commands.Initialization ;
using Arent3d.Architecture.Routing.Mechanical.App.Commands.PassPoint ;
using Arent3d.Architecture.Routing.Mechanical.App.Commands.Routing ;
using Arent3d.Architecture.Routing.Mechanical.App.Commands.Rack ;
using Arent3d.Revit.UI.Attributes ;

namespace Arent3d.Architecture.Routing.Mechanical.App
{
  partial class RoutingAppUI
  {
#if TTE_BUILD
    [Tab( "Mechanical.App.Routing.TabName", VisibilityMode = TabVisibilityMode.NormalDocument )]
    private static class RoutingTab
    {
      [Panel( "arent3d.architecture.routing.init", TitleKey = "Mechanical.App.Panels.Routing.Initialize" )]
      private static class InitPanel
      {
        [Button( typeof( InitializeCommand ), InitializeButton = true )]
        private static class InitializeCommandButton { }
      }      
      
      [Panel("arent3d.architecture.routing.routing", TitleKey = "Mechanical.App.Panels.Routing.Routing" )]
      private static class RoutingPanel
      {
        [Button( typeof( SimplePickRoutingCommand ), OnlyInitialized = true )]
        private static class SimplePickRoutingCommandButton { }

        [Button( typeof( PickAndReRouteCommand ), OnlyInitialized = true )]
        private static class PickAndReRouteCommandButton { }

        [Button( typeof( AllReRouteCommand ), OnlyInitialized = true )]
        private static class AllReRouteCommandButton { }

        [Button( typeof( EraseSelectedRoutesCommand ), OnlyInitialized = true )]
        private static class EraseSelectedRoutesCommandButton { }

        [Button( typeof( EraseAllRoutesCommand ), OnlyInitialized = true )]
        private static class EraseAllRoutesCommandButton { }

        [Button( typeof( ShowFromToTreeCommand ), OnlyInitialized = true )]
        private static class ShowFromToTreeCommandButton { }
      }
      
      [Panel( "arent3d.architecture.routing.tte", TitleKey = "Mechanical.App.Panels.Routing.TTE" )]
      private static class TTEPanel
      {
        [Button( typeof( CreateFASUAndVAVAutomaticallyCommand ), OnlyInitialized = true )]
        private static class CreateFASUAndVAVAutomaticallyCommandButton { }

        [Button( typeof( AutoRoutingVavCommand ), OnlyInitialized = true )]
        private static class AutoRoutingVavCommandButton { }
        
        [Button( typeof( AdjustDuctSizeCommand ), OnlyInitialized = true )]
        private static class AdjustDuctSizeCommandButton { }
        
        [Button( typeof( AutoRoutingAnemostatCommand ), OnlyInitialized = true )]
        private static class AutoRoutingAnemostatCommandButton { }    
      }      
    }
#else
    [Tab( "Mechanical.App.Routing.TabName", VisibilityMode = TabVisibilityMode.NormalDocument )]
    private static class RoutingTab
    {
      [Panel( "arent3d.architecture.routing.init", TitleKey = "Mechanical.App.Panels.Routing.Initialize" )]
      private static class InitPanel
      {
        [Button( typeof( InitializeCommand ), InitializeButton = true )]
        private static class InitializeCommandButton { }

        [Button( typeof( ShowRoutingViewsCommand ), OnlyInitialized = true )]
        private static class ShowRoutingViewsCommandButton { }
      }

      [Panel("arent3d.architecture.routing.routing", TitleKey = "Mechanical.App.Panels.Routing.Routing" )]
      private static class RoutingPanel
      {
        [Button( typeof( PickRoutingCommand ), OnlyInitialized = true )]
        private static class PickRoutingCommandButton { }
        
        [Button( typeof( SimplePickRoutingCommand ), OnlyInitialized = true )]
        private static class SimplePickRoutingCommandButton { }

        [Button( typeof( PickAndReRouteCommand ), OnlyInitialized = true )]
        private static class PickAndReRouteCommandButton { }

        [Button( typeof( AllReRouteCommand ), OnlyInitialized = true )]
        private static class AllReRouteCommandButton { }

        [Button( typeof( InsertPassPointCommand ), OnlyInitialized = true )]
        private static class InsertPassPointCommandButton { }

        [Button( typeof( EraseSelectedRoutesCommand ), OnlyInitialized = true )]
        private static class EraseSelectedRoutesCommandButton { }

        [Button( typeof( EraseAllRoutesCommand ), OnlyInitialized = true )]
        private static class EraseAllRoutesCommandButton { }

        [Button( typeof( ReplaceFromToCommand ), OnlyInitialized = true )]
        private static class ReplaceFromToCommandButton { }

        [Button( typeof( ShowFrom_ToWindowCommand ), OnlyInitialized = true )]
        private static class ShowFrom_ToWindowCommandButton { }

        [Button( typeof( ShowFromToTreeCommand ), OnlyInitialized = true )]
        private static class ShowFromToTreeCommandButton { }
      }

      [Panel( "arent3d.architecture.routing.rack", TitleKey = "Mechanical.App.Panels.Routing.Racks" )]
      private static class RackPanel
      {
        [Button( typeof( ImportRacksCommand ), OnlyInitialized = true )]
        private static class ImportRacksCommandButton { }

        [Button( typeof( ExportRacksCommand ), OnlyInitialized = true )]
        private static class ExportRacksCommandButton { }

        [Button( typeof( EraseAllRacksCommand ), OnlyInitialized = true )]
        private static class EraseAllRacksCommandButton { }

        [Button( typeof( RackGuideCommand ), OnlyInitialized = true )]
        private static class RackGuidCommandButton { }
        
        [Button( typeof( CreateFASUAndVAVAutomaticallyCommand ), OnlyInitialized = true )]
        private static class CreateFASUAndVAVAutomaticallyCommandButton { }

        [Button( typeof( AutoRoutingVavCommand ), OnlyInitialized = true )]
        private static class AutoRoutingVavCommandButton { }
        
        [Button( typeof( AdjustDuctSizeCommand ), OnlyInitialized = true )]
        private static class AdjustDuctSizeCommandButton { }     
        
        [Button( typeof( AutoRoutingAnemostatCommand ), OnlyInitialized = true )]
        private static class AutoRoutingAnemostatCommandButton { }
      }

      [Panel( "arent3d.architecture.routing.monitor", TitleKey = "Mechanical.App.Panels.Routing.Monitor" )]
      private static class MonitorPanel
      {
        [Button( typeof( MonitorSelectionCommand ), AvailabilityType = typeof( Commands.Enabler.MonitorSelectionCommandEnabler ) )]
        private static class MonitorSelectionCommandButton { }
      }

      [Panel( "arent3d.architecture.rc.debug", TitleKey = "App.Panels.Rc.Debug" )]
      private static class DebugPanel
      {

        [Button( typeof( UninitializeCommand ), OnlyInitialized = true )]
        private static class UnInitializeCommandButton
        {
        }
      }
    }
    
#endif
    
  }
}