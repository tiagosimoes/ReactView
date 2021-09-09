using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using ReactViewControl;
using WebViewControl;
using static Example.Avalonia.WebServer.SerializedObject;

namespace Example.Avalonia {

    internal class ExtendedReactViewFactory : ReactViewFactory {

        public override ResourceUrl DefaultStyleSheet => Settings.IsBorderLessPreference ?
            new ResourceUrl(typeof(ExtendedReactViewFactory).Assembly, "Generated", "DefaultStyleSheet.css") :
            new ResourceUrl(typeof(ExtendedReactViewFactory).Assembly, "Generated", "DefaultStyleSheetWithBorders.css");

        public override IViewModule[] InitializePlugins() {
            return new[]{
                new ViewPlugin()
            };
        }

        public override bool ShowDeveloperTools => false;

        public override bool EnableViewPreload => true;

        public override int MaxNativeMethodsParallelCalls => 1;

        delegate object CallTargetMethod(Func<object> target);
        static readonly Dictionary<string, object> registeredObjects = new Dictionary<string, object>();
        static readonly Dictionary<string, CallTargetMethod> registeredObjectInterceptMethods = new Dictionary<string, CallTargetMethod>();
        private CountdownEvent JavascriptPendingCalls { get; } = new CountdownEvent(1);

        public override bool RegisterWebJavaScriptObject(string name, object objectToBind, Func<Func<object>, object> interceptCall, bool executeCallsInUI = false) {
            if (registeredObjects.ContainsKey(name)) {
                return false;
            }

            // TODO TCS: Check if this is needded
            //if (executeCallsInUI) {
            //    return RegisterWebJavaScriptObject(name, objectToBind, target => ExecuteInUI<object>(target), false);
            //}

            if (interceptCall == null) {
                interceptCall = target => target();
            }

            object CallTargetMethod(Func<object> target) {
                // TODO TCS: Check if this is needded
                //if (isDisposing) {
                //    return null;
                //}
                try {
                    JavascriptPendingCalls.AddCount();
                    //if (isDisposing) {
                    //    // check again, to avoid concurrency problems with dispose
                    //    return null;
                    //}
                    return interceptCall(target);
                } finally {
                    JavascriptPendingCalls.Signal();
                }
            }


            var serializedObject = SerializeObject(objectToBind);
            registeredObjects[name] = objectToBind;
            registeredObjectInterceptMethods[name] = CallTargetMethod;
            var text = $"{{ \"RegisterObjectName\": \"{name}\", \"Object\": {serializedObject} }}";
            if (WebServer.ServerApiStartup.ProcessMessage == null) {
                WebServer.ServerApiStartup.ProcessMessage = ReceiveMessage;
            }
            _ = WebServer.ServerApiStartup.SendWebSocketMessage(text);
            return true;
        }

        public void ReceiveMessage(string text) {
            var methodCall = DeserializeMethodCall(text);
            var obj = registeredObjects[methodCall.ObjectName];
            var callTargetMethod = registeredObjectInterceptMethods[methodCall.ObjectName];
            callTargetMethod(() => ExecuteMethod(obj, methodCall));
        }

        public override void UnregisterWebJavaScriptObject(string name) {
            var text = $"{{ \"UnregisterObjectName\": \"{name}\"}}";
            _ = WebServer.ServerApiStartup.SendWebSocketMessage(text);
            registeredObjects.Remove(name);
        }

        public override void ExecuteWebScriptFunctionWithSerializedParams(string functionName, params object[] args) {
            functionName = functionName.Replace("embedded://webview/", "/");
            var text = $"{{ \"Execute\": \"{JsonEncodedText.Encode(functionName)}\", \"Arguments\": {JsonSerializer.Serialize(args)} }}";
            _ = WebServer.ServerApiStartup.SendWebSocketMessage(text);
        }

#if DEBUG
        public override bool EnableDebugMode => true;

        public override Uri DevServerURI => null;



#endif
    }
}
