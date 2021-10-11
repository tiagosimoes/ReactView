using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Sample.Avalonia.WebServer {
    class TypeDTO {
        public string AssemblyName;
        public string ClassName;

        public static TypeDTO FromType(Type type) {
            return new TypeDTO() {
                AssemblyName = type.Assembly.FullName,
                ClassName = type.FullName
            };
        }
    }

    class MethodSignatureDTO {
        public TypeDTO ReturnType;
        public string MethodName;
        public TypeDTO[] ParameterTypes;

        public static MethodSignatureDTO FromMethod(MethodInfo method) {
            return new MethodSignatureDTO() {
                ReturnType = TypeDTO.FromType(method.ReturnType),
                MethodName = method.Name,
                ParameterTypes = method.GetParameters().Select(t => TypeDTO.FromType(t.ParameterType)).ToArray()
            };
        }
    }

    class SerializedObject {
        public TypeDTO type;
        public MethodSignatureDTO[] methods;
        public SerializedObject(object obj) {
            type = TypeDTO.FromType(obj.GetType());
            var methodSignatures = new List<MethodSignatureDTO>();
            foreach (var method in obj.GetType().GetMethods()) {
                methodSignatures.Add(MethodSignatureDTO.FromMethod(method));
            }
            methods = methodSignatures.ToArray();
        }

        public static string SerializeObject(object obj) {
            var serializedObject = new SerializedObject(obj);
            return JsonSerializer.Serialize(serializedObject, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true});
        }

        [Serializable]
        public struct MethodCall {
            public string ObjectName;
            public string MethodName;
            public object Args;
            public int CallKey;
        }

        public static MethodCall DeserializeMethodCall(string text) {
            return JsonSerializer.Deserialize<MethodCall>(text, new JsonSerializerOptions { IncludeFields = true });
        }

        private static object GetJSONValue(JsonElement elem, Type type) {
            return elem.ValueKind switch {
                JsonValueKind.Null => null,
                JsonValueKind.Number => elem.GetDouble(),
                JsonValueKind.False => false,
                JsonValueKind.True => true,
                JsonValueKind.Undefined => null,
                JsonValueKind.String => elem.GetString(),
                JsonValueKind.Array => elem.EnumerateArray().Select(o => GetJSONValue(o, type)).ToArray(),
                JsonValueKind.Object => JsonSerializer.Deserialize(elem.GetRawText(), type),
                _ => throw new NotImplementedException(),
            };
            throw new NotImplementedException();
        }

        internal static object ExecuteMethod(object obj, MethodCall methodCall) {
            var method = obj.GetType().GetMethod(methodCall.MethodName);
            object[] arguments = Array.Empty<object>();
            if (methodCall.Args is JsonElement elem) {
                var type = method.GetParameters().FirstOrDefault()?.ParameterType;
                if (elem.ValueKind == JsonValueKind.Array) {
                    arguments = (object[]) GetJSONValue(elem, type);
                } else {    
                    arguments = new[] {GetJSONValue(elem, type) };
                }
            }
            if (method.ReturnType == typeof(void)) {
                obj.GetType().GetMethod(methodCall.MethodName).Invoke(obj, arguments);
                return null;
            } else {
                return obj.GetType().GetMethod(methodCall.MethodName).Invoke(obj, arguments);
            }
        }
    }
}
