using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Avalonia.Threading;

namespace ReactViewControl.WebServer {
    public class TypeDTO {
        public string AssemblyName;
        public string ClassName;

        public static TypeDTO FromType(Type type) {
            return new TypeDTO() {
                AssemblyName = type.Assembly.FullName,
                ClassName = type.FullName
            };
        }
    }

    public class MethodSignatureDTO {
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
            return JsonSerializer.Serialize(serializedObject, new JsonSerializerOptions { IncludeFields = true, WriteIndented = false});
        }

        [Serializable]
        public struct MethodCall {
#pragma warning disable CS0649
            public string ObjectName;
#pragma warning disable CS0649
            public string MethodName;
#pragma warning disable CS0649
            public object Args;
#pragma warning disable CS0649
            public int CallKey;
        }

        public static MethodCall DeserializeMethodCall(string text) {
            return JsonSerializer.Deserialize<MethodCall>(text, new JsonSerializerOptions { IncludeFields = true });
        }

        public struct EvaluateResult {
#pragma warning disable CS0649
            public string EvaluateKey;
#pragma warning disable CS0649
            public JsonElement EvaluatedResult;
        }

        public static EvaluateResult DeserializeEvaluateResult(string text) {
            return JsonSerializer.Deserialize<EvaluateResult>(text, new JsonSerializerOptions { IncludeFields = true });
        }

         public struct MenuClickedObject {
            public int MenuClicked;
        }

        internal static MenuClickedObject DeserializeMenuClicked(string text) {
            return JsonSerializer.Deserialize<MenuClickedObject>(text, new JsonSerializerOptions { IncludeFields = true });
        }

        public struct WindowSettings {
            public double Width;
            public double Height;
            public string Title;
            public bool IsResizable;
        }

    }
}
