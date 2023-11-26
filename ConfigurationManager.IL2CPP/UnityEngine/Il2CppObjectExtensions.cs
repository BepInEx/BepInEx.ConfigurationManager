using System.Reflection;

namespace UnityEngine
{
    internal static class Il2CppObjectExtensions
    {
        public static T Instantiate<T>(this T self) where T : Object =>
            Object.Instantiate(self as Object) as T;

        public static T Instantiate<T>(this T self, bool instantiateUnityEngineObject = false) where T : Il2CppSystem.Object
        {
            T instance = null;
            if (self is not null)
            {
                if (self is not Object)
                {
                    Type type = self.GetType();
                    ConstructorInfo constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, Array.Empty<ParameterModifier>());
                    if (constructor is not null)
                    {
                        instance = constructor.Invoke(null) as T;
                        if (instance is not null)
                        {
                            PropertyInfo[] properties = type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
                            for (int i = 0; i < properties.Length; i++)
                            {
                                if (!properties[i].CanWrite || !properties[i].CanRead || properties[i].Name.Contains('_'))
                                    continue;
                                object value = properties[i].GetValue(self);
                                if (value is Il2CppSystem.Object)
                                    value = (value as Il2CppSystem.Object).Instantiate(instantiateUnityEngineObject) ?? value;
                                properties[i].SetValue(instance, value);
                            }
                        }
                    }
                }
                else
                {
                    instance = instantiateUnityEngineObject
                        ? Object.Instantiate(self as Object) as T
                        : self;
                }
            }
            return instance;
        }
    }
}
