﻿using System.Collections.Generic ;
using System.Linq ;
using System.Runtime.InteropServices ;
using Arent3d.Architecture.Routing.Storable.Model ;
using Arent3d.Revit ;
using Autodesk.Revit.DB ;
using Autodesk.Revit.DB.ExtensibleStorage ;

namespace Arent3d.Architecture.Routing.Storable
{
  [Guid( "8820c33e-cdbe-4540-abad-4aa30cc39506" )]
  [StorableVisibility( AppInfo.VendorId )]
  public class CeedStorable : StorableBase
  {
    public const string StorableName = "CeeD Model" ;
    private const string CeedModelField = "CeeDModel" ;
    private const string CeedModelUsedField = "CeeDModelUsed" ;
    private const string IsShowCeedModelNumberField = "IsShowCeedModelNumber" ;
    private const string IsShowConditionField = "IsShowCondition" ;
    private const string ConnectorFamilyUploadField = "ConnectorFamilyUpload" ;
    private const string IsShowOnlyUsingCodeField = "IsShowOnlyUsingCode" ;
    private const string IsDiffField = "IsDiff" ;

    public List<CeedModel> CeedModelData { get ; set ; }
    public List<CeedModel> CeedModelUsedData { get ; set ; }
    public bool IsShowCeedModelNumber { get ; set ; }
    public bool IsShowCondition { get ; set ; }
    public List<string> ConnectorFamilyUploadData { get ; set ; }
    public bool IsShowOnlyUsingCode { get ; set ; }
    
    public bool IsDiff { get ; set ; }
    

    public CeedStorable( DataStorage owner ) : base( owner, false )
    {
      CeedModelData = new List<CeedModel>() ;
      CeedModelUsedData = new List<CeedModel>() ;
      ConnectorFamilyUploadData = new List<string>() ;
      IsShowCondition = true ;
    }

    public CeedStorable( Document document ) : base( document, false )
    {
      CeedModelData = new List<CeedModel>() ;
      CeedModelUsedData = new List<CeedModel>() ;
      ConnectorFamilyUploadData = new List<string>() ;
      IsShowCondition = true ;
    }

    protected override void LoadAllFields( FieldReader reader )
    {
      CeedModelData = reader.GetArray<CeedModel>( CeedModelField ).ToList() ;
      CeedModelUsedData = reader.GetArray<CeedModel>( CeedModelUsedField ).ToList() ;
      IsShowCeedModelNumber = reader.GetSingle<bool>( IsShowCeedModelNumberField ) ;
      IsShowCondition = reader.GetSingle<bool>( IsShowConditionField ) ;
      ConnectorFamilyUploadData = reader.GetArray<string>( ConnectorFamilyUploadField ).ToList() ;
      IsShowOnlyUsingCode = reader.GetSingle<bool>( IsShowOnlyUsingCodeField ) ;
      IsDiff = reader.GetSingle<bool>( IsDiffField ) ;
    }

    protected override void SaveAllFields( FieldWriter writer )
    {
      writer.SetArray( CeedModelField, CeedModelData ) ;
      writer.SetArray( CeedModelUsedField, CeedModelUsedData ) ;
      writer.SetSingle(  IsShowCeedModelNumberField, IsShowCeedModelNumber) ;
      writer.SetSingle(  IsShowConditionField, IsShowCondition) ;
      writer.SetArray( ConnectorFamilyUploadField, ConnectorFamilyUploadData ) ;
      writer.SetSingle(  IsShowOnlyUsingCodeField, IsShowOnlyUsingCode) ;
      writer.SetSingle(  IsDiffField, IsDiff) ;
    }

    protected override void SetupAllFields( FieldGenerator generator )
    {
      generator.SetArray<CeedModel>( CeedModelField ) ;
      generator.SetArray<CeedModel>( CeedModelUsedField ) ;
      generator.SetSingle<bool>( IsShowCeedModelNumberField  ) ;
      generator.SetSingle<bool>( IsShowConditionField  ) ;
      generator.SetArray<string>( ConnectorFamilyUploadField ) ;
      generator.SetSingle<bool>( IsShowOnlyUsingCodeField  ) ;
      generator.SetSingle<bool>( IsDiffField  ) ;
    }

    public override string Name => StorableName ;
  }
}