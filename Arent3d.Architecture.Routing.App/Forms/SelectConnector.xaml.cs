﻿using Arent3d.Utility ;
using Autodesk.Revit.DB ;
using System ;
using System.Collections.ObjectModel ;
using System.ComponentModel ;
using System.Runtime.CompilerServices ;
using System.Windows ;
using System.Linq ;
using Arent3d.Revit ;

namespace Arent3d.Architecture.Routing.App.Forms
{
  /// <summary>
  /// Interaction logic for SelectConnector.xaml
  /// </summary>
  public partial class SelectConnector : Window
  {
    private readonly Connector? _firstConnector ;

    public ObservableCollection<ConnectorInfoClass> ConnectorList { get ; } = new() ;

    public SelectConnector( FamilyInstance familyInstance, Connector? firstConnector = null )
    {
      InitializeComponent() ;

      _firstConnector = firstConnector ;

      var familyInstanceTransform = familyInstance.GetTotalTransform() ;
      var familyDocument = familyInstance.Document.EditFamily( familyInstance.Symbol.Family ) ;
      foreach ( var conn in familyDocument.GetAllElements<ConnectorElement>().Where( IsTargetConnectorElement ) ) {
        ConnectorList.Add( new ConnectorInfoClass( familyInstance, familyInstanceTransform, conn, _firstConnector ) ) ;
      }
      ConnectorList.Add( new ConnectorInfoClass( familyInstance ) ) ;

      this.Left = 0 ;
      this.Top += 10 ;
    }

    private static bool IsTargetConnectorElement( ConnectorElement el )
    {
      return el.Domain switch
      {
        Domain.DomainPiping => true,
        Domain.DomainHvac => true,
        _ => false
      } ;
    }

    public class ConnectorInfoClass : INotifyPropertyChanged
    {
      public bool IsEnabled { get ; }

      private bool _isSelected = false ;

      public bool IsSelected
      {
        get => _isSelected ;
        set
        {
          if ( false == IsEnabled ) return ;

          _isSelected = value ;
          NotifyPropertyChanged() ;
        }
      }

      public event PropertyChangedEventHandler? PropertyChanged ;

      private Instance Instance { get ; }

      private XYZ? ConnectorPosition { get ; }
      private ConnectorElement? ConnectorElement { get ; }

      /// <summary>
      /// ConnectorInfo for the center of an instance.
      /// </summary>
      /// <param name="instance">Instance.</param>
      public ConnectorInfoClass( Instance instance )
      {
        Instance = instance ;
        ConnectorElement = null ;
        ConnectorPosition = null ;

        IsEnabled = true ;
      }

      public ConnectorInfoClass( FamilyInstance familyInstance, Transform familyInstanceTransform, ConnectorElement connectorElement, Connector? firstElement )
      {
        Instance = familyInstance ;
        ConnectorElement = connectorElement ;
        ConnectorPosition = familyInstanceTransform.OfPoint( connectorElement.Origin ) ;

        IsEnabled = ( null == firstElement ) || ( HasCompatibleType( firstElement ) && firstElement.HasSameShape( ConnectorElement ) ) ;
      }

      private void NotifyPropertyChanged( [CallerMemberName] string propertyName = "" )
      {
        PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) ) ;
      }

      public override string ToString()
      {
        if ( null == ConnectorElement ) return "Origin of this element" ;
        return $"{ConnectorElement.Name} - {UnitUtils.ConvertFromInternalUnits( ConnectorElement.Radius, UnitTypeId.Millimeters ) * 2} - {ConnectorElement.get_Parameter( BuiltInParameter.RBS_PIPE_FLOW_DIRECTION_PARAM )?.AsValueString()}" ;
      }

      public Connector? GetConnector()
      {
        if ( false == IsEnabled || false == IsSelected ) return null ;
        if ( null == ConnectorElement ) return null ;

        return Instance.GetConnectors().FirstOrDefault( IsMatch ) ;
      }

      private bool IsMatch( Connector connector )
      {
        return HasCompatibleType( connector ) && HasSamePosition( connector ) ;
      }

      private bool HasCompatibleType( Connector connector )
      {
        if ( false == connector.IsAnyEnd() ) return false ;
        if ( null == ConnectorElement ) return true ;

        if ( connector.Domain != ConnectorElement.Domain ) return false ;

        if ( ConnectorElement.SystemClassification != MEPSystemClassification.Global ) {
          if ( connector.GetSystemTypeName() != ConnectorElement.SystemClassification.ToString() ) return false ;
        }

        return true ;
      }

      private bool HasSamePosition( Connector connector )
      {
        return connector.Origin.IsAlmostEqualTo( ConnectorPosition ) ;
      }
    }

    private void Button_Click( object sender, RoutedEventArgs e )
    {
      this.DialogResult = true ;
      this.Close() ;
    }

    public Connector? GetSelectedConnector()
    {
      return ConnectorList.Select( cic => cic.GetConnector() ).NonNull().FirstOrDefault() ;
    }
  }
}