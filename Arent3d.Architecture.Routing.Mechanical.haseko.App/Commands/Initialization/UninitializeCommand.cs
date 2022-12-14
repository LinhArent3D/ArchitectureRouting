using Arent3d.Architecture.Routing.AppBase.Commands.Initialization ;
using Arent3d.Architecture.Routing.Storages.Extensions ;
using Arent3d.Revit.UI ;
using Autodesk.Revit.Attributes ;
using Autodesk.Revit.DB ;
using ImageType = Arent3d.Revit.UI.ImageType ;

namespace Arent3d.Architecture.Routing.Mechanical.Haseko.App.Commands.Initialization
{
  [Transaction( TransactionMode.Manual )]
  [DisplayNameKey( "Mechanical.Haseko.App.Commands.Initialization.UnnitializeCommand", DefaultString = "Erase all addin data" )]
  [Image( "resources/Initialize.png", ImageType = ImageType.Large )]
  public class UninitializeCommand : UninitializeCommandBase
  {
    protected override void UnSetup( Document document )
    {
      base.UnSetup( document ) ;
      document.DeleteEntireSchema();
    }
  }
}