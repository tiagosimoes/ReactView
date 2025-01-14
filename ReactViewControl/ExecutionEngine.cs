﻿using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using ReactViewControl.WebServer;

namespace ReactViewControl {

    internal class ExecutionEngine : IExecutionEngine {

        internal const string ModulesObjectName = "__Modules__";

        private string id;
        private string frameName;
        private ServerView serverView;

        private ConcurrentQueue<Tuple<IViewModule, string, object[]>> PendingExecutions { get; } = new ConcurrentQueue<Tuple<IViewModule, string, object[]>>();

        private string FormatMethodInvocation(IViewModule module, string methodCall) {
            return ModulesObjectName + "(\"" + frameName + "\",\"" + id + "\",\"" + module.Name + "\")." + methodCall;
        }

        public void ExecuteMethod(IViewModule module, string methodCall, params object[] args) {
            module.Host?.HandledBeforeExecuteMethod();

            if (serverView != null) {
                var method = FormatMethodInvocation(module, methodCall);
                serverView.ExecuteWebScriptFunctionWithSerializedParams(method, args);
            } else {
                PendingExecutions.Enqueue(Tuple.Create(module, methodCall, args));
            }
        }

        public T EvaluateMethod<T>(IViewModule module, string methodCall, params object[] args) => EvaluateMethodAsync<T>(module, methodCall, args).Result;

        public Task<T> EvaluateMethodAsync<T>(IViewModule module, string methodCall, params object[] args) {
            if (serverView == null || module.Name == "UIEditor.view") {
                // Disable all UIEditor evaluations as they can give deadlocks
                // TODO TCS need to review this later
                return Task.FromResult<T>(default);
            }
            module.Host?.HandledBeforeExecuteMethod();
            var method = FormatMethodInvocation(module, methodCall);
            return serverView.EvaluateScriptFunctionWithSerializedParams<T>(method, args);
        }

        public void Start(ServerView serverView, string frameName, string id) {
            this.id = id;
            this.frameName = frameName;
            this.serverView = serverView;
            while (true) {
                if (PendingExecutions.TryDequeue(out var pendingScript)) {
                    var method = FormatMethodInvocation(pendingScript.Item1, pendingScript.Item2);
                    serverView.ExecuteWebScriptFunctionWithSerializedParams(method, pendingScript.Item3);
                } else {
                    // nothing else to execute
                    break;
                }
            }
        }

        public void MergeWorkload(IExecutionEngine executionEngine) {
            if (this != executionEngine && executionEngine is ExecutionEngine otherEngine) {
                var pendingExecutions = otherEngine.PendingExecutions.ToArray();
                foreach (var execution in pendingExecutions) {
                    PendingExecutions.Enqueue(execution);
                }
            }
        }
    }
}