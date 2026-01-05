using System;
using System.Reflection;
using HarmonyLib;

internal class HarmonyFieldProxy<T>
{
	private readonly FieldInfo Field;

	public HarmonyFieldProxy(Type type, string name)
	{
		Field = AccessTools.Field(type, name);
	}

	public T Get(object instance)
	{
		return (T)Field.GetValue(instance);
	}

	public void Set(object instance, T value)
	{
		Field.SetValue(instance, value);
	}
}
