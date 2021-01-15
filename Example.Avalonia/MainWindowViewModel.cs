using System.ComponentModel;
using System.Reactive;
using ReactiveUI;
using WebViewControl;

namespace Example.Avalonia {

    internal class MainWindowViewModel : ReactiveObject {

        public MainWindowViewModel(ExampleView view) {
            CutCommand = ReactiveCommand.Create(() => {
                view.EditCommands.Cut();
            });

            CopyCommand = ReactiveCommand.Create(() => {
                view.EditCommands.Copy();
            });

            PasteCommand = ReactiveCommand.Create(() => {
                view.EditCommands.Paste();
            });

            UndoCommand = ReactiveCommand.Create(() => {
                view.EditCommands.Undo();
            });

            RedoCommand = ReactiveCommand.Create(() => {
                view.EditCommands.Redo();
            });

            SelectAllCommand = ReactiveCommand.Create(() => {
                view.EditCommands.SelectAll();
            });

            DeleteCommand = ReactiveCommand.Create(() => {
                view.EditCommands.Delete();
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
