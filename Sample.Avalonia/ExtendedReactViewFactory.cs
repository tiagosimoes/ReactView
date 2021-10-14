using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Avalonia.Threading;
using Microsoft.AspNetCore.Http;
using ReactViewControl;
using WebViewControl;
using static Sample.Avalonia.WebServer.SerializedObject;

namespace Sample.Avalonia {

    internal class ExtendedReactViewFactory : ReactViewFactory {

        public override ResourceUrl DefaultStyleSheet =>
            new ResourceUrl(typeof(ExtendedReactViewFactory).Assembly, "Generated", Settings.IsLightTheme ? "LightTheme.css" : "DarkTheme.css");

        public override IViewModule[] InitializePlugins() {
            return new[]{
            new ViewPlugin()
        };
        }

        public override bool ShowDeveloperTools => false;

        public override bool EnableViewPreload => false;

        public override int MaxNativeMethodsParallelCalls => 1;

        delegate object CallTargetMethod(Func<object> target);

        private ReactViewRender.NativeAPI nativeAPI;
        readonly Dictionary<string, object> registeredObjects = new Dictionary<string, object>();
        readonly Dictionary<string, CallTargetMethod> registeredObjectInterceptMethods = new Dictionary<string, CallTargetMethod>();
        private CountdownEvent JavascriptPendingCalls { get; } = new CountdownEvent(1);

        public override bool RegisterWebJavaScriptObject(string name, object objectToBind, Func<Func<object>, object> interceptCall, bool executeCallsInUI = false) {
            if (registeredObjects.ContainsKey(name)) {
                return false;
            }

            // TODO TCS: Check if this is needded
            if (executeCallsInUI) {
                return RegisterWebJavaScriptObject(name, objectToBind, target => ExecuteInUI<object>(target), false);
            }

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
            if (registeredObjects.Count == 1) {
                nativeAPI = (ReactViewRender.NativeAPI) objectToBind;
                WebServer.ServerApiStartup.NewNativeObject(name, this);
            }
            var text = $"{{ \"RegisterObjectName\": \"{name}\", \"Object\": {serializedObject} }}";
            _ = SendWebSocketMessage(text);
            return true;
        }


        private T ExecuteInUI<T>(Func<T> action) {
            return Dispatcher.UIThread.ExecuteInUIThread<T>(action);
        }

        internal Stream GetCustomResource(string path, out string extension) {
            return nativeAPI.ViewRender.GetCustomResource(path, out extension);
        }

        public void ReceiveMessage(string text) {
            var methodCall = DeserializeMethodCall(text);
            var obj = registeredObjects[methodCall.ObjectName];
            var callTargetMethod = registeredObjectInterceptMethods[methodCall.ObjectName];
            callTargetMethod(() => {
                var result = ExecuteMethod(obj, methodCall);
                if (obj.GetType().GetMethod(methodCall.MethodName).ReturnType != typeof(void)) {
                    ReturnValue(methodCall.CallKey, result);
                }
                return result;
            });
        }

        private WebSocket webSocket;

        private async System.Threading.Tasks.Task SendWebSocketMessage(string message) {
            var stream = Encoding.UTF8.GetBytes(message);
            while (webSocket == null) {
                webSocket = await WaitForNextWebSocket();
            }
            await webSocket.SendAsync(new ArraySegment<byte>(stream), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async System.Threading.Tasks.Task<WebSocket> WaitForNextWebSocket() {
            while (webSocket == null) {
                await System.Threading.Tasks.Task.Delay(500);
                webSocket = WebServer.ServerApiStartup.NextWebSocket;
            }
            WebServer.ServerApiStartup.NextWebSocket = null;
            _ = OnWebSocketMessageReceived(webSocket);
            return webSocket;
        }

        private async System.Threading.Tasks.Task OnWebSocketMessageReceived(WebSocket webSocket) {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue) {
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                ReceiveMessage(text);
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }

        public override void UnregisterWebJavaScriptObject(string name) {
            var text = $"{{ \"UnregisterObjectName\": \"{name}\"}}";
            _ = SendWebSocketMessage(text);
            registeredObjects.Remove(name);
        }

        public override void ExecuteWebScriptFunctionWithSerializedParams(string functionName, params object[] args) {
            functionName = functionName.Replace("embedded://webview/", "/");
            var text = $"{{ \"Execute\": \"{JsonEncodedText.Encode(functionName)}\", \"Arguments\": {JsonSerializer.Serialize(args)} }}";
            _ = SendWebSocketMessage(text);
        }
        private void ReturnValue(float callKey, object value) {
            var text = $"{{ \"ReturnValue\": \"{callKey}\", \"Arguments\": {JsonSerializer.Serialize(value)} }}";
            _ = SendWebSocketMessage(text);
        }

#if DEBUG
        public override bool EnableDebugMode => true;

        public override Uri DevServerURI => null;



#endif
    }

    internal static class DispatcherExtensions {
        private static Action<Exception> unhandledExceptionHandler;
        public static R ExecuteInUIThread<R>(this Dispatcher dispatcher, Func<R> func, DispatcherPriority priority = DispatcherPriority.Normal) {
            try {
                if (dispatcher.CheckAccess()) {
                    return func();
                }

                return dispatcher.InvokeAsync(func, priority).Result;
            } catch (Exception e) {
                unhandledExceptionHandler?.Invoke(e);
                throw;
            }
        }
    }
}
