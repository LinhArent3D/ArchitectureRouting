using System.Collections.Generic ;
using System.ComponentModel ;
using System.Reflection ;
using Arent3d.Architecture.Routing.AppBase.Manager ;
using Arent3d.Architecture.Routing.AppBase.Updater ;
using Arent3d.Architecture.Routing.StorableCaches ;
using Arent3d.Revit ;
using Arent3d.Revit.UI ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.DB.Events ;
using Autodesk.Revit.UI ;
using Autodesk.Revit.UI.Events ;

namespace Arent3d.Architecture.Routing.AppBase
{
  /// <summary>
  /// Entry point of auto routing application. This class calls UI initializers.
  /// </summary>
  [Revit.RevitAddin( AppInfo.ApplicationGuid )]
  [DisplayName( AppInfo.ApplicationName )]
  public class RoutingApp : ExternalApplicationBase
  {
    protected override string GetLanguageDirectoryPath()
    {
      return GetDefaultLanguageDirectoryPath( Assembly.GetExecutingAssembly() ) ;
    }

    protected override IAppUIBase? CreateAppUI( UIControlledApplication application )
    {
      return RoutingAppUI.Create( application ) ;
    }

    protected override void RegisterEvents( UIControlledApplication application )
    {
    }

    protected override void UnregisterEvents( UIControlledApplication application )
    {
    }

    protected override void OnDocumentListenStarted( Document document )
    {
      DocumentMapper.Register( document ) ;
      FromToTreeManager.Instance.OnDocumentOpened() ;
    }

    protected override void OnDocumentListenFinished( Document document )
    {
      DocumentMapper.Unregister( document ) ;
    }

    protected override void OnDocumentChanged( Document document, DocumentChangedEventArgs e )
    {
      FromToTreeManager.Instance.OnDocumentChanged( e ) ;
    }

    protected override void OnApplicationViewChanged( Document document, ViewActivatedEventArgs e )
    {
      FromToTreeManager.Instance.OnViewActivated( e ) ;
    }

    protected override IEnumerable<IDocumentUpdateListener> GetUpdateListeners()
    {
      yield return new RoutingUpdateListener() ;
    }
  }
}