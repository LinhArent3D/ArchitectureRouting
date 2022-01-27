﻿using Arent3d.Architecture.Routing.Storable.Model ;
using Arent3d.Revit ;
using Arent3d.Utility.Serialization ;
using Autodesk.Revit.DB ;

namespace Arent3d.Architecture.Routing.Storable.StorableConverter
{
  [StorableConverterOf( typeof( ConnectorFamilyTypeModel ) )]
  public class ConnectorFamilyTypeModelStorableConvert : StorableConverterBase<ConnectorFamilyTypeModel>
  {
    private enum SerializeField
    {
      Base64Images,
      FamilyTypeName,
      ConnectorFamilyTypeName
    }

    protected override ConnectorFamilyTypeModel Deserialize( Element storedElement, IDeserializerObject deserializerObject )
    {
      var deserializer = deserializerObject.Of<SerializeField>() ;

      var base64Images = deserializer.GetString( SerializeField.Base64Images ) ;
      var familyTypeName = deserializer.GetString( SerializeField.FamilyTypeName ) ;
      var connectorFamilyTypeName = deserializer.GetString( SerializeField.ConnectorFamilyTypeName ) ;

      return new ConnectorFamilyTypeModel( base64Images, familyTypeName, connectorFamilyTypeName ) ;
    }

    protected override ISerializerObject Serialize( Element storedElement, ConnectorFamilyTypeModel customTypeValue )
    {
      var serializerObject = new SerializerObject<SerializeField>() ;

      serializerObject.AddNonNull( SerializeField.Base64Images, customTypeValue.Base64Images ) ;
      serializerObject.AddNonNull( SerializeField.FamilyTypeName, customTypeValue.FamilyTypeName ) ;
      serializerObject.AddNonNull( SerializeField.ConnectorFamilyTypeName, customTypeValue.ConnectorFamilyTypeName ) ;

      return serializerObject ;
    }
  }
}