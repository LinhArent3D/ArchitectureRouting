﻿using System.Windows ;
using System.Windows.Controls ;
using Arent3d.Architecture.Routing.AppBase.ViewModel ;
using Autodesk.Revit.DB ;

namespace Arent3d.Architecture.Routing.AppBase.Forms
{
  public partial class ContentDisplayDialog
  {
    public ContentDisplayDialog( PickUpViewModel pickUpViewModel )
    { 
      InitializeComponent() ;
      DataContext = pickUpViewModel ;
    }

    private void DataGrid_LoadingRow( object sender, DataGridRowEventArgs e )
    {
      e.Row.Header = ( e.Row.GetIndex() + 1 ).ToString() ;
    }

    private void CloseDialog( object sender, RoutedEventArgs e )
    {
      DialogResult = true ;
      Close() ;
    }
  }
 
  public abstract class DesignPickUpViewModel : PickUpViewModel
  {
    protected DesignPickUpViewModel( Document document ) : base( document )
    {
    }
  }
}