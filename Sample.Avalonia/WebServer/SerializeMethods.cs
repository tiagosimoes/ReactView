using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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

        public class MethodCall {
            public string ObjectName;
            public string MethodName;
            public object Args;
        }

        public static MethodCall DeserializeMethodCall(string text) {
            return JsonSerializer.Deserialize<MethodCall>(text, new JsonSerializerOptions { IncludeFields = true });
        }

        internal static object ExecuteMethod(object obj, MethodCall methodCall) {
            var method = obj.GetType().GetMethod(methodCall.MethodName);
            object[] arguments = Array.Empty<object>();
            if (methodCall.Args is JsonElement elem) {
                if (elem.ValueKind == JsonValueKind.String) {
                    arguments = new[] { elem.GetString() };
                } else {
                    throw new NotImplementedException(); //TODO TCS
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
