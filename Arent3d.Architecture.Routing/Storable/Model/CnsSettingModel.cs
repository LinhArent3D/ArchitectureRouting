﻿using System ;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Arent3d.Architecture.Routing.Storable.Model
{
  public class CnsSettingModel : INotifyPropertyChanged, ICloneable
  {
    private int _sequence;
    public int Sequence 
    { 
      get => _sequence;
      set
      {
        _sequence = value;
        OnPropertyChanged();
      }
    }

    private int _index;
    public int Index 
    { 
      get => _index;
      set
      {
        _index = value;
        OnPropertyChanged();
      }
    }
    public string CategoryName { get; set; }

    public CnsSettingModel(int sequence, string categoryName)
    {
      _sequence = sequence;
      CategoryName = categoryName;
      _index = sequence ;
    }
    
    public bool Equals( CnsSettingModel other )
    {
      return other != null &&
             Sequence == other.Sequence &&
             CategoryName == other.CategoryName ;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public object Clone()
    {
      return this.MemberwiseClone();
    }
  }
}
