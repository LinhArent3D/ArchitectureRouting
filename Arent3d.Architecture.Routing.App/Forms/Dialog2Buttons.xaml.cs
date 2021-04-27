﻿using System.Windows ;
using System.Windows.Controls ;

namespace Arent3d.Architecture.Routing.App.Forms
{
  public delegate void ClickEventHandler( object sender, RoutedEventArgs e ) ;

  public partial class Dialog2Buttons : UserControl
  {
    public string LeftButton
    {
      get { return (string) GetValue( LeftButtonProperty ) ; }
      set { SetValue( LeftButtonProperty, value ) ; }
    }

    public static readonly DependencyProperty LeftButtonProperty = DependencyProperty.Register( "LeftButtont", 
      typeof( string ), 
      typeof( Dialog2Buttons ), 
      new PropertyMetadata( "OK" ) ) ;

    public string RightButton
    {
      get { return (string) GetValue( RightButtonProperty ) ; }
      set { SetValue( RightButtonProperty, value ) ; }
    }

    public static readonly DependencyProperty RightButtonProperty = DependencyProperty.Register( "RightButton", 
      typeof( string ), 
      typeof( Dialog2Buttons ), 
      new PropertyMetadata( "Cancel" ) ) ;


    public Dialog2Buttons()
    {
      InitializeComponent() ;
    }

    public event ClickEventHandler? LeftOnClick ;

    public event ClickEventHandler? RightOnClick ;

    private void Left_OnClick( object sender, RoutedEventArgs e )
    {
      if ( LeftOnClick != null ) {
        LeftOnClick( this, e ) ;
      }
    }

    private void Right_OnClick( object sender, RoutedEventArgs e )
    {
      if ( RightOnClick != null ) {
        RightOnClick( this, e ) ;
      }
    }
  }
}