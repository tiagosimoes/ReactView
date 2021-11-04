using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using static ReactViewControl.WebServer.ServerView;

namespace ReactViewControl.WebServer {
    public class ServerViewsAggregator {

        public string StarterURL;
        public int AggregatorWindowHashCode;
        readonly List<ServerView> serverViews = new List<ServerView>();
        static readonly Dictionary<string, ServerViewsAggregator> aggregatorWindows = new Dictionary<string, ServerViewsAggregator>();

        public static ServerViewsAggregator GetServerViewsAggregatorForSession(string sessionId) {
            if (!aggregatorWindows.ContainsKey(sessionId)) {
                aggregatorWindows[sessionId] = new ServerViewsAggregator();
            }
            return aggregatorWindows[sessionId];
        }

        public static ServerViewsAggregator GetServerViewsAggregatorForWindow(int aggregatorWindowHashCode) {
            return aggregatorWindows.Values.FirstOrDefault(serverAPI => serverAPI.AggregatorWindowHashCode == aggregatorWindowHashCode);
        }

        public Stream GetCustomResource(string nativeobjectname, string customPath, out string extension) {
            var nativeObject = serverViews.FirstOrDefault(conn => conn.NativeAPIName == nativeobjectname);
            return nativeObject.GetCustomResource(customPath, out extension);
        }
        public void AddSocket(WebSocket socket, TaskCompletionSource<object> socketFinishedTcs, string path) {
            var serverView = serverViews.Find(conn => conn.NativeAPIName == path.Substring(1));
            serverView.SetSocket(socket);
        }

        readonly static string ReactViewResources = "ReactViewResources";
        readonly static string CustomResourcePath = "custom/resource";

        public static Func<ReactViewRender, int> GetAggregatorWindowHashCodeFroControl { get; set; }
        public static Action<int> CloseAggregatorWindow { get; set; }

        internal static void NewNativeObject(ServerView serverView, ReactViewRender viewRenderer) {
            var aggregatorHashCode = GetAggregatorWindowHashCodeFroControl(viewRenderer);
            var serverViewAggregator = GetServerViewsAggregatorForWindow(aggregatorHashCode);
            serverViewAggregator.serverViews.Add(serverView);
            string url = $"/{ReactViewResources}/index.html?./&true&__Modules__&{serverView.NativeAPIName}&{CustomResourcePath}";
            if (serverViewAggregator.serverViews.Count == 1) {
                serverViewAggregator.StarterURL = url;
            } else {
                _ = Task.Run(() => {
                    while (serverViewAggregator.LastConnectionWithActivity() == null) {
                        Task.Delay(1);
                    }
                    Operation operation;
                    switch (serverView.GetViewName()) {
                        case "AIContextSuggestionsMenuView":
                        case "ReactViewHostForPlugins":
                        case "OutSystemsBrowserLoadingView":
                        case "DialogView":
                            operation = Operation.OpenURLInPopup;
                            break;
                        case "TooltipView":
                            // TODO TCS, fix tooltips 
                            return;
                        case "WorkspaceView":
                        default:
                            operation = Operation.OpenURL;
                            break;
                    }
                    _ = serverViewAggregator.LastConnectionWithActivity().SendWebSocketMessage(operation, url);
                });
            }
        }

        public string LastNativeObjectName() {
            return LastConnectionWithActivity()?.NativeAPIName;
        }

        private ServerView LastConnectionWithActivity() {
            return serverViews.Where(serverView => serverView.IsSocketOpen()).OrderByDescending(serverView => serverView.LastActivity).FirstOrDefault();
        }

        internal static void CloseSocket(ServerView serverView) {
            foreach (var agregatorWindow in aggregatorWindows.Values) {
                agregatorWindow.Close(serverView);
            }
        }

        internal void Close(ServerView serverView) {
            serverViews.Remove(serverView);
            if (serverViews.Count == 0) {
                CloseAggregatorWindow(AggregatorWindowHashCode);
            }
        }


        public void OpenURL(Uri uri) {
            while (LastConnectionWithActivity() == null) {
                Task.Delay(1);
            }
            _ = LastConnectionWithActivity().SendWebSocketMessage(Operation.OpenURL, uri.AbsoluteUri);
        }

    }
}
