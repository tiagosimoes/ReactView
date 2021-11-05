﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ReactViewControl.WebServer {
    public class ServerView {

        delegate object CallTargetMethod(Func<object> target);
        public static Action<ServerView> NewNativeObject { get; set; }
        public static Action<ServerView> CloseSocket { get; set; }


        private ReactViewRender.NativeAPI nativeAPI;
        public string NativeAPIName;
        public DateTime LastActivity;
        readonly Dictionary<string, object> registeredObjects = new Dictionary<string, object>();
        readonly Dictionary<string, CallTargetMethod> registeredObjectInterceptMethods = new Dictionary<string, CallTargetMethod>();
        private CountdownEvent JavascriptPendingCalls { get; } = new CountdownEvent(1);
        public enum Operation {
            RegisterObjectName,
            UnregisterObjectName,
            EvaluateScriptFunctionWithSerializedParams,
            Execute,
            ResizePopup,
            ReturnValue,
            OpenURL,
            OpenURLInPopup,
            OpenTooltip,
            OpenContextMenu,
            CloseWindow,
            MenuClicked
        }

        public bool RegisterWebJavaScriptObject(string name, object objectToBind, Func<Func<object>, object> interceptCall, bool executeCallsInUI = false) {
            if (registeredObjects.ContainsKey(name)) {
                return false;
            }

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

            registeredObjects[name] = objectToBind;
            registeredObjectInterceptMethods[name] = CallTargetMethod;
            if (registeredObjects.Count == 1) {
                nativeAPI = (ReactViewRender.NativeAPI)objectToBind;
                NativeAPIName = name;
                NewNativeObject(this);
            }
            _ = SendWebSocketMessageRegister(name, objectToBind);
            return true;
        }

        public ReactView Host {
            get {
                while (nativeAPI.ViewRender.Host == null) {
                    Task.Delay(10);
                }
                return nativeAPI.ViewRender.Host;
            }
        }

        public void UnregisterWebJavaScriptObject(string name) {
            if (registeredObjects.ContainsKey(name)) {
                _ = SendWebSocketMessage(Operation.UnregisterObjectName, name);
                registeredObjects.Remove(name);
            }
        }

        public bool IsSocketOpen() {
            return webSocket != null && webSocket.State == WebSocketState.Open;
        }

        public void ExecuteWebScriptFunctionWithSerializedParams(string functionName, params object[] args) {
            functionName = functionName.Replace("embedded://webview/", "/");
            _ = SendWebSocketMessage(Operation.Execute, functionName, JsonSerializer.Serialize(args));
        }
        private void ReturnValue(float callKey, object value) {
            _ = SendWebSocketMessage(Operation.ReturnValue, callKey.ToString(), JsonSerializer.Serialize(value, new JsonSerializerOptions() { MaxDepth = 512 }));
        }

        private readonly Dictionary<string, JsonElement> evaluateResults = new Dictionary<string, JsonElement>();

        internal Task<T> EvaluateScriptFunctionWithSerializedParams<T>(string method, object[] args) {
            var evaluateKey = Guid.NewGuid().ToString();
            _ = SendWebSocketMessageEvaluate(method, evaluateKey, args);
            while (!evaluateResults.ContainsKey(evaluateKey)) {
                Task.Delay(50);
            }
            return Task.FromResult(JsonSerializer.Deserialize<T>((evaluateResults[evaluateKey]).GetRawText()));
        }

        internal void OpenContextMenu(ContextMenu menu) {
            IEnumerable<object> menuItems = GetMenuItems(menu.Items);
            _ = SendWebSocketMessage(Operation.OpenContextMenu, JsonSerializer.Serialize(menuItems, new JsonSerializerOptions() { IncludeFields = true }));
        }

        private static IEnumerable<object> GetMenuItems(System.Collections.IEnumerable items) {
            return items.OfType<object>().Where(item => (item as MenuItem)?.IsVisible ?? true).Select(item =>
                    (item is MenuItem menuItem) ? (object)new WebMenuItem(menuItem) : new WebMenuSeparator()
            );
        }

        class WebMenuItem {
            public string Header;
            public bool IsEnabled;
            public bool IsVisible;
            public Image Icon;
            public bool IsBold;
            public int HashCode;
            public IEnumerable<object> Items;

            public WebMenuItem(MenuItem menuItem) {
                Header = (string)menuItem.Header;
                IsEnabled = menuItem.IsEnabled;
                IsVisible = menuItem.IsVisible;
                HashCode = menuItem.GetHashCode();
                //Icon = (Image)menuItem.Icon;
                IsBold = menuItem.FontWeight == Avalonia.Media.FontWeight.Bold;
                Items = GetMenuItems(menuItem.Items);
            }
        }

        struct WebMenuSeparator { }

        private T ExecuteInUI<T>(Func<T> action) {
            return Dispatcher.UIThread.CheckAccess() ? action() : Dispatcher.UIThread.InvokeAsync(action).Result;
        }

        public Stream GetCustomResource(string path, out string extension) {
            return nativeAPI.ViewRender.GetCustomResource(path, out extension);
        }

        public void OpenURL(string url, bool inPopup = false) {
            _ = SendWebSocketMessage(inPopup? Operation.OpenURLInPopup: Operation.OpenURL, url);
        }

        public void SetPopupDimensions() {
            while (!nativeAPI.ViewRender.IsInitialized) {
                Task.Delay(10);
            }
            var windowSettings = ExecuteInUI(() => {
                var window = (Window)nativeAPI.ViewRender.Host.Parent;
                return new SerializedObject.WindowSettings() {
                    Height = window.Height,
                    Width = window.Width,
                    Title = window.Title,
                    IsResizable = window.CanResize
                };
            });

            _ = SendWebSocketMessage(Operation.ResizePopup, JsonSerializer.Serialize(windowSettings, new JsonSerializerOptions() { IncludeFields = true }));
        }

        private void ReceiveMessage(string text) {
            if (text.StartsWith("{\"EvaluateKey\"")) {
                var evaluateResult = SerializedObject.DeserializeEvaluateResult(text);
                evaluateResults[evaluateResult.EvaluateKey] = evaluateResult.EvaluatedResult;
            } else if (text.StartsWith($"{{\"{Operation.CloseWindow}\"")) {
                CloseWindow();
            } else if (text.StartsWith($"{{\"{Operation.MenuClicked}\"")) {
                var menuClicked = SerializedObject.DeserializeMenuClicked(text);
                ClickOnMenuItem(menuClicked);
            } else {
                var methodCall = SerializedObject.DeserializeMethodCall(text);
                ExecuteMethod(methodCall);
            }
        }

        private void ExecuteMethod(SerializedObject.MethodCall methodCall) {
            var obj = registeredObjects[methodCall.ObjectName];
            var callTargetMethod = registeredObjectInterceptMethods[methodCall.ObjectName];
            callTargetMethod(() => {
                var result = SerializedObject.ExecuteMethod(obj, methodCall);
                if (obj.GetType().GetMethod(methodCall.MethodName).ReturnType != typeof(void)) {
                    ReturnValue(methodCall.CallKey, result);
                }
                return result;
            });
        }

        private void CloseWindow() {
            Dispatcher.UIThread.InvokeAsync(() => {
                var window = nativeAPI.ViewRender.Host.Parent as Window;
                window.Close();
            });
        }

        private void ClickOnMenuItem(SerializedObject.MenuClickedObject menuClicked) {
            Dispatcher.UIThread.InvokeAsync(() => {
                IEnumerable<MenuItem> GetAllSubMenuItems(MenuItem menuItem) {
                    return new[] { menuItem }.Concat(
                        menuItem.Items.OfType<MenuItem>().SelectMany(subMenuItem => GetAllSubMenuItems(subMenuItem)));
                }
                var menu = nativeAPI.ViewRender.Host.ContextMenu;
                var allMenuItems = menu.Items.OfType<MenuItem>().SelectMany(item => GetAllSubMenuItems(item));
                var clickedMenuItem = allMenuItems.FirstOrDefault(item => item.GetHashCode() == menuClicked.MenuClicked);
                if (clickedMenuItem != null) {
                    clickedMenuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                }
                menu.Close();
            });
        }

        private WebSocket webSocket;
        private TaskCompletionSource<object> socketFinished;

        internal async Task SendWebSocketMessage(Operation operation, string value, string arguments = "[]") {
            await SendWebSocketMessage($"{{ \"{operation}\": \"{JsonEncodedText.Encode(value)}\", \"Arguments\":{arguments} }}");
        }

        private async Task SendWebSocketMessageRegister(string name, object objectToBind) {
            await SendWebSocketMessage($"{{ \"{Operation.RegisterObjectName}\": \"{name}\", \"Object\": {SerializedObject.SerializeObject(objectToBind)} }}");
        }

        private async Task SendWebSocketMessageEvaluate(string method, string evaluateKey, object[] args) {
            await SendWebSocketMessage($"{{ \"{Operation.EvaluateScriptFunctionWithSerializedParams}\": \"{JsonEncodedText.Encode(method)}\", \"EvaluateKey\":\"{evaluateKey}\", \"Arguments\":{JsonSerializer.Serialize(args)} }}");
        }

        private async Task SendWebSocketMessage(string message) {
            var stream = Encoding.UTF8.GetBytes(message);
            while (webSocket == null) {
                await Task.Delay(10);
            }
            if (IsSocketOpen()) {
                await webSocket.SendAsync(new ArraySegment<byte>(stream), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private async Task ListenForMessages(WebSocket webSocket) {
            var buffer = new byte[1024 * 1024];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue) {
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                LastActivity = DateTime.Now;
                ReceiveMessage(text);
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            CloseSocket(this);
            socketFinished.SetResult(0);
        }

        public void SetSocket(WebSocket socket, TaskCompletionSource<object> socketFinishedTcs) {
            webSocket = socket;
            socketFinished = socketFinishedTcs;
            _ = ListenForMessages(webSocket);
        }
    }
}
