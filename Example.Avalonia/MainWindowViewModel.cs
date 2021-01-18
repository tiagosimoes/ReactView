using System;
using System.ComponentModel;
using System.Reactive;
using Avalonia.Threading;
using ReactiveUI;
using WebViewControl;

namespace Example.Avalonia {

    internal class MainWindowViewModel : ReactiveObject {

        public MainWindowViewModel(Func<View> selectedViewGetter) {

            CutCommand = ReactiveCommand.Create(() => {
                Dispatcher.UIThread.InvokeAsync(() => selectedViewGetter.Invoke().view.EditCommands.Cut());
            });

            CopyCommand = ReactiveCommand.Create(() => {
                Dispatcher.UIThread.InvokeAsync(() => selectedViewGetter.Invoke().view.EditCommands.Copy());
            });

            PasteCommand = ReactiveCommand.Create(() => {
                Dispatcher.UIThread.InvokeAsync(() => selectedViewGetter.Invoke().view.EditCommands.Paste());
            });

            UndoCommand = ReactiveCommand.Create(() => {
                Dispatcher.UIThread.InvokeAsync(() => selectedViewGetter.Invoke().view.EditCommands.Undo());
            });

            RedoCommand = ReactiveCommand.Create(() => {
                Dispatcher.UIThread.InvokeAsync(() => selectedViewGetter.Invoke().view.EditCommands.Redo());
            });

            SelectAllCommand = ReactiveCommand.Create(() => {
                Dispatcher.UIThread.InvokeAsync(() => selectedViewGetter.Invoke().view.EditCommands.SelectAll());
            });

            DeleteCommand = ReactiveCommand.Create(() => {
                Dispatcher.UIThread.InvokeAsync(() => selectedViewGetter.Invoke().view.EditCommands.Delete());
            });
        }

        public ReactiveCommand<Unit, Unit> CutCommand { get; }

        public ReactiveCommand<Unit, Unit> CopyCommand { get; }

        public ReactiveCommand<Unit, Unit> PasteCommand { get; }

        public ReactiveCommand<Unit, Unit> UndoCommand { get; }

        public ReactiveCommand<Unit, Unit> RedoCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }

        public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
    }
}
