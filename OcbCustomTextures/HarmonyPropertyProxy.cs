using System;
using System.Reflection;
using HarmonyLib;

internal class HarmonyPropertyProxy<T>
{
	private readonly PropertyInfo Property;

	public HarmonyPropertyProxy(Type type, string name)
	{
		Property = AccessTools.Property(type, name);
	}

	public T Get(object instance)
	{
		return (T)Property.GetValue(instance);
	}

	public void Set(object instance, T value)
	{
		Property.SetValue(instance, value);
	}
}
