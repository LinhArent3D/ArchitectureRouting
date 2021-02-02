using System ;
using Autodesk.Revit.DB ;

namespace Arent3d.Revit.UI
{
  public enum AppUIUpdateType
  {
    Start,
    Finish,
    Change,
  }

  public interface IAppUIBase : IDisposable
  {
    void UpdateUI( Document document, AppUIUpdateType updateType ) ;
  }
}