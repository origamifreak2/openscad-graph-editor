using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GodotExt;
using JetBrains.Annotations;
using OpenScadGraphEditor.History;
using OpenScadGraphEditor.Library;
using OpenScadGraphEditor.Library.External;
using OpenScadGraphEditor.Nodes;
using OpenScadGraphEditor.Nodes.Reroute;
using OpenScadGraphEditor.Refactorings;
using OpenScadGraphEditor.Utils;
using OpenScadGraphEditor.Widgets;
using OpenScadGraphEditor.Widgets.AddDialog;
using OpenScadGraphEditor.Widgets.ImportDialog;
using OpenScadGraphEditor.Widgets.InvokableRefactorDialog;
using OpenScadGraphEditor.Widgets.ProjectTree;
using OpenScadGraphEditor.Widgets.VariableRefactorDialog;

namespace OpenScadGraphEditor
{
    [UsedImplicitly]
    public class GraphEditor : Control
    {
        // TODO: this class gets _really_ big.

        private AddDialog _addDialog;
        private QuickActionsPopup _quickActionsPopup;
        private Control _editingInterface;
        private TextEdit _textEdit;
        private FileDialog _fileDialog;
        private ProjectTree _projectTree;
        private ImportDialog _importDialog;
        private Button _undoButton;
        private Button _redoButton;

        private string _currentFile;

        private bool _dirty;
        private Button _forceSaveButton;
        private Label _fileNameLabel;
        private TabContainer _tabContainer;
        private ScadProject _currentProject;
        private HistoryStack _currentHistoryStack;
        private GlobalLibrary _rootResolver;
        private InvokableRefactorDialog _invokableRefactorDialog;
        private VariableRefactorDialog _variableRefactorDialog;
        private readonly List<IAddDialogEntry> _addDialogEntries = new List<IAddDialogEntry>();
        private bool _codeChanged;

        public override void _Ready()
        {
            OS.LowProcessorUsageMode = true;
            _rootResolver = new GlobalLibrary();

            _tabContainer = this.WithName<TabContainer>("TabContainer");

            _addDialog = this.WithName<AddDialog>("AddDialog");
            _quickActionsPopup = this.WithName<QuickActionsPopup>("QuickActionsPopup");
            _importDialog = this.WithName<ImportDialog>("ImportDialog");
            _importDialog.OnNewImportRequested += OnNewImportRequested;

            _invokableRefactorDialog = this.WithName<InvokableRefactorDialog>("InvokableRefactorDialog");
            _invokableRefactorDialog.InvokableModificationRequested +=
                (description, refactorings) => PerformRefactorings($"Change {description.Name}", refactorings);
            _invokableRefactorDialog.InvokableCreationRequested +=
                // create the invokable, then open it's graph in a new tab.
                (description, refactorings) => PerformRefactorings($"Create {description.Name}", refactorings,
                    () => Open(_currentProject.FindDefiningGraph(description)));

            _variableRefactorDialog = this.WithName<VariableRefactorDialog>("VariableRefactorDialog");
            _variableRefactorDialog.RefactoringsRequested += (refactorings) => PerformRefactorings("Rename variable", refactorings);

            _editingInterface = this.WithName<Control>("EditingInterface");
            _textEdit = this.WithName<TextEdit>("TextEdit");
            _fileDialog = this.WithName<FileDialog>("FileDialog");
            _fileNameLabel = this.WithName<Label>("FileNameLabel");
            _forceSaveButton = this.WithName<Button>("ForceSaveButton");
            _forceSaveButton.Connect("pressed")
                .To(this, nameof(SaveChanges));


            _projectTree = this.WithName<ProjectTree>("ProjectTree");

            _projectTree.ItemActivated += Open;
            _projectTree.ItemContextMenuRequested += OnItemContextMenuRequested;

            _undoButton = this.WithName<Button>("UndoButton");
            _undoButton.Connect("pressed")
                .To(this, nameof(Undo));

            _redoButton = this.WithName<Button>("RedoButton");
            _redoButton.Connect("pressed")
                .To(this, nameof(Redo));

            this.WithName<Button>("AddExternalReferenceButton")
                .Connect("pressed")
                .To(this, nameof(OnImportScadFile));

            this.WithName<Button>("NewButton")
                .Connect("pressed")
                .To(this, nameof(OnNewButtonPressed));

            this.WithName<Button>("PreviewButton")
                .Connect("toggled")
                .To(this, nameof(OnPreviewToggled));

            this.WithName<Button>("OpenButton")
                .Connect("pressed")
                .To(this, nameof(OnOpenFilePressed));

            this.WithName<Button>("SaveAsButton")
                .Connect("pressed")
                .To(this, nameof(OnSaveAsPressed));

            this.WithName<Button>("AddModuleButton")
                .Connect("pressed")
                .To(this, nameof(OnAddModulePressed));


            this.WithName<Button>("AddFunctionButton")
                .Connect("pressed")
                .To(this, nameof(OnAddFunctionPressed));

            this.WithName<Button>("AddVariableButton")
                .Connect("pressed")
                .To(this, nameof(OnAddVariablePressed));


            MarkDirty(true);

            this.WithName<Timer>("Timer")
                .Connect("timeout")
                .To(this, nameof(SaveChanges));

            OnNewButtonPressed();
        }

        private void OnNewImportRequested(string path, IncludeMode includeMode)
        {
            OnRefactoringRequested($"Add reference to {path}", new AddExternalReferenceRefactoring(path, includeMode));
        }

        private void OnItemContextMenuRequested(ProjectTreeEntry entry, Vector2 mousePosition)
        {
            var actions = new List<QuickAction>();
            var title = $"Delete {entry.Title}";
            if (entry is ScadInvokableTreeEntry invokableTreeEntry)
            {
                switch (invokableTreeEntry.Description)
                {
                    case FunctionDescription functionDescription:
                        if (_currentProject.IsDefinedInThisProject(functionDescription))
                        {
                            actions.Add(new QuickAction(title,
                                () => OnRefactoringRequested(title, new DeleteInvokableRefactoring(functionDescription))));
                        }

                        break;
                    case ModuleDescription moduleDescription:
                        if (_currentProject.IsDefinedInThisProject(moduleDescription))
                        {
                            actions.Add(new QuickAction(title,
                                () => OnRefactoringRequested(title, new DeleteInvokableRefactoring(moduleDescription))));
                        }

                        break;
                }
            }

            if (entry is ScadVariableTreeEntry scadVariableListEntry)
            {
                if (_currentProject.IsDefinedInThisProject(scadVariableListEntry.Description))
                {
                    actions.Add(new QuickAction(title,
                        () => OnRefactoringRequested(
                            title, new DeleteVariableRefactoring(scadVariableListEntry.Description))));
                }
            }

            if (entry is ExternalReferenceTreeEntry externalReferenceTreeEntry &&
                !externalReferenceTreeEntry.Description.IsTransitive)
            {
                var removeReferenceTitle = $"Remove reference to {entry.Title}";
                actions.Add(new QuickAction(removeReferenceTitle,
                    () => OnRefactoringRequested(
                        removeReferenceTitle, new DeleteExternalReferenceRefactoring(externalReferenceTreeEntry.Description))));
            }

            _quickActionsPopup.Open(mousePosition, actions);
        }

        private void Open(ProjectTreeEntry entry)
        {
            if (entry is ScadInvokableTreeEntry invokableTreeEntry
                && _currentProject.IsDefinedInThisProject(invokableTreeEntry.Description))
            {
                Open(_currentProject.FindDefiningGraph(invokableTreeEntry.Description));
            }
        }

        private void Clear()
        {
            _currentProject?.Discard();
            _currentHistoryStack = null;
            _currentFile = null;
            _fileNameLabel.Text = "<not yet saved to a file>";
        }


        private void RefreshControls()
        {
            _undoButton.Disabled = !_currentHistoryStack.CanUndo(out var undoDescription);
            _undoButton.HintTooltip = $"Undo : {undoDescription}";
            
            _redoButton.Disabled = !_currentHistoryStack.CanRedo(out var redoDescription);
            _redoButton.HintTooltip = $"Redo : {redoDescription}";
            
            _projectTree.Setup(new List<ProjectTreeEntry>() {new RootProjectTreeEntry(_currentProject)});


            // Fill the Add Dialog Entries.

            _addDialogEntries.Clear();

            _addDialogEntries.AddRange(
                BuiltIns.LanguageLevelNodes
                    .Select(it => new NodeBasedEntry(
                        Resources.ScadBuiltinIcon,
                        () => NodeFactory.Build(it),
                        AddNode
                    ))
            );

            _addDialogEntries.AddRange(
                BuiltIns.Functions
                    .Select(it => new NodeBasedEntry(
                        Resources.FunctionIcon,
                        () => NodeFactory.Build<FunctionInvocation>(it),
                        AddNode
                    ))
            );

            _addDialogEntries.AddRange(
                BuiltIns.Modules
                    .Select(it => new NodeBasedEntry(
                        Resources.ModuleIcon,
                        () => NodeFactory.Build<ModuleInvocation>(it),
                        AddNode
                    )));


            // also add entries for functions and modules defined in the project
            _addDialogEntries.AddRange(
                _currentProject.Functions
                    .Select(it => new NodeBasedEntry(
                        Resources.FunctionIcon,
                        () => NodeFactory.Build<FunctionInvocation>(it.Description),
                        AddNode
                    ))
            );

            _addDialogEntries.AddRange(
                _currentProject.Modules
                    .Select(it => new NodeBasedEntry(
                        Resources.ModuleIcon,
                        () => NodeFactory.Build<ModuleInvocation>(it.Description),
                        AddNode
                    ))
            );

            // add getter and setters for all variables
            _addDialogEntries.AddRange(
                _currentProject.Variables
                    .Select(it => new NodeBasedEntry(
                        Resources.VariableIcon,
                        () => NodeFactory.Build<SetVariable>(it),
                        AddNode
                    ))
            );

            _addDialogEntries.AddRange(
                _currentProject.Variables
                    .Select(it => new NodeBasedEntry(
                        Resources.VariableIcon,
                        () => NodeFactory.Build<GetVariable>(it),
                        AddNode
                    ))
            );


            // add call entries for all externally referenced functions and modules
            foreach (var reference in _currentProject.ExternalReferences)
            {
                _addDialogEntries.AddRange(
                    reference.Functions
                        .Select(it => new NodeBasedEntry(
                                Resources.FunctionIcon,
                                () => NodeFactory.Build<FunctionInvocation>(it),
                                AddNode
                            )
                        )
                );

                _addDialogEntries.AddRange(
                    reference.Modules
                        .Select(it => new NodeBasedEntry(
                                Resources.ModuleIcon,
                                () => NodeFactory.Build<ModuleInvocation>(it),
                                AddNode
                            )
                        )
                );
            }
        }

        private void Undo()
        {
            GdAssert.That(_currentHistoryStack.CanUndo(out _), "Cannot undo");
            var editorState = _currentHistoryStack.Undo(_currentProject);
            RestoreEditorState(editorState);
        }

        private void Redo()
        {
            GdAssert.That(_currentHistoryStack.CanRedo(out _), "Cannot redo");
            var editorState = _currentHistoryStack.Redo(_currentProject);
            RestoreEditorState(editorState);
        }


        private void OnImportScadFile()
        {
            _importDialog.OpenForNewImport(_currentFile);
        }


        private void AddNode(RequestContext context, NodeBasedEntry entry)
        {
            AddNode(context, entry.CreateNode());
        }

        private void AddNode(RequestContext context, ScadNode node)
        {
            node.Offset = context.LastReleasePosition;
            var otherNode = context.SourceNode ?? context.DestinationNode;
            var isIncoming = context.SourceNode != null;
            var otherPort = context.LastPort;
            
            PerformRefactorings("Add node", new AddNodeRefactoring(context.Source, node, otherNode, otherPort, isIncoming));
        }

        private ScadGraphEdit Open(IScadGraph toOpen)
        {
            // check if it is already open
            for (var i = 0; i < _tabContainer.GetChildCount(); i++)
            {
                var existingEditor = _tabContainer.GetChild<ScadGraphEdit>(i);
                if (existingEditor.Description.Id == toOpen.Description.Id)
                {
                    _tabContainer.CurrentTab = i;
                    return existingEditor;
                }
            }

            // if not, open a new tab
            var editor = Prefabs.New<ScadGraphEdit>();
            AttachTo(editor);
            editor.Name = toOpen.Description.NodeNameOrFallback;
            editor.MoveToNewParent(_tabContainer);
            _currentProject.TransferData(toOpen, editor);
            toOpen.Discard();
            _tabContainer.CurrentTab = _tabContainer.GetChildCount() - 1;
            editor.FocusEntryPoint();
            return editor;
        }

        private void OnAddFunctionPressed()
        {
            _invokableRefactorDialog.OpenForNewFunction();
        }

        private void OnAddVariablePressed()
        {
            _variableRefactorDialog.OpenForNewVariable();
        }

        private void OnAddModulePressed()
        {
            _invokableRefactorDialog.OpenForNewModule();
        }

        private void OnRefactoringRequested(string humanReadableDescription, Refactoring refactoring)
        {
            PerformRefactorings(humanReadableDescription, new[] {refactoring});
        }

        private void PerformRefactorings(string description, params Refactoring[] refactorings)
        {
            PerformRefactorings(description, (IEnumerable<Refactoring>) refactorings);
        }
        
        private void PerformRefactorings(string description, IEnumerable<Refactoring> refactorings,
            params Action[] after)
        {
            var context = new RefactoringContext(_currentProject);
            context.PerformRefactorings(refactorings, after);
            // important, the snapshot must be made _after_ the changes.
            _currentHistoryStack.AddSnapshot(description, _currentProject, GetEditorState());
            RefreshControls();
            MarkDirty(true);
        }

        private void OnNewButtonPressed()
        {
            Clear();
            _currentProject = new ScadProject(_rootResolver);
            Open(_currentProject.MainModule);
            // important, this needs to be done after the main module is opened
            _currentHistoryStack = new HistoryStack(_currentProject, GetEditorState());
            RefreshControls();
        }

        private EditorState GetEditorState()
        {
            // create a list of currently open tabs
            var tabs =
                _tabContainer.GetChildNodes<ScadGraphEdit>()
                    .Select((it, idx) =>
                        new EditorOpenTab(it.Description.Id, it.ScrollOffset));
            return new EditorState(tabs, _tabContainer.CurrentTab);
        }

        private void RestoreEditorState(EditorState editorState)
        {
            // close all open tabs
            foreach (var tab in _tabContainer.GetChildNodes<ScadGraphEdit>())
            {
                tab.Discard();
            }

            // open all tabs that were open back then
            foreach (var openTab in editorState.OpenTabs)
            {
                var invokableId = openTab.InvokableId;
                var invokable = _currentProject.AllDeclaredInvokables.First(it => it.Description.Id == invokableId);
                var editor = Open(invokable);
                editor.ScrollOffset = openTab.ScrollOffset;
            }

            // and finally select the tab that was selected
            _tabContainer.CurrentTab = editorState.CurrentTabIndex;
            
            RefreshControls();
        }


        private void AttachTo(ScadGraphEdit editor)
        {
            editor.RefactoringsRequested += PerformRefactorings;
            editor.NodePopupRequested += OnNodePopupRequested;
            editor.ItemDataDropped += OnItemDataDropped;
            editor.AddDialogRequested += OnAddDialogRequested;
        }

        private void OnAddDialogRequested(RequestContext context)
        {
            // when shift is pressed this means we want to have a reroute node.
            if (Input.IsKeyPressed((int) KeyList.Shift))
            {
                AddNode(context, NodeFactory.Build<RerouteNode>());
                return;
            }

            // otherwise let the user choose a node.
            _addDialog.Open(_addDialogEntries, context);
        }

        private void OnItemDataDropped(ScadGraphEdit graph, ProjectTreeEntry entry, Vector2 mousePosition,
            Vector2 virtualPosition)
        {
            // did we drag an invokable from the list to the graph?
            if (entry is ScadInvokableTreeEntry invokableListEntry)
            {
                // then add a new node calling the invokable.
                var description = invokableListEntry.Description;
                ScadNode node;
                if (description is FunctionDescription functionDescription)
                {
                    node = NodeFactory.Build<FunctionInvocation>(functionDescription);
                }
                else if (description is ModuleDescription moduleDescription)
                {
                    node = NodeFactory.Build<ModuleInvocation>(moduleDescription);
                }
                else
                {
                    throw new InvalidOperationException("Unknown invokable type.");
                }

                node.Offset = virtualPosition;
                OnRefactoringRequested("Add node", new AddNodeRefactoring(graph, node));
            }

            // did we drag a variable from the list to the graph?
            if (entry is ScadVariableTreeEntry variableListEntry)
            {
                // in case of a variable we can either _get_ or _set_ the variable
                // so we will need to open a popup menu to let the user choose.
                var getNode = NodeFactory.Build<GetVariable>(variableListEntry.Description);
                getNode.Offset = virtualPosition;
                var setNode = NodeFactory.Build<SetVariable>(variableListEntry.Description);
                setNode.Offset = virtualPosition;

                var actions = new List<QuickAction>
                {
                    new QuickAction($"Get {variableListEntry.Description.Name}",
                        () => OnRefactoringRequested("Add node", new AddNodeRefactoring(graph, getNode))),
                    new QuickAction($"Set {variableListEntry.Description.Name}",
                        () => OnRefactoringRequested("Add node", new AddNodeRefactoring(graph, setNode)))
                };

                _quickActionsPopup.Open(mousePosition, actions);
            }
        }

        private void OnNodePopupRequested(ScadGraphEdit editor, ScadNode node, Vector2 position)
        {
            // build a list of quick actions that include all refactorings that would apply to the selected node
            var actions = UserSelectableNodeRefactoring
                .GetApplicable(editor, node)
                .Select(it => new QuickAction(it.Title, () => OnRefactoringRequested(it.Title, it)));


            // if the node references some invokable, add an action to open the refactor dialog for this invokable.
            if (node is IReferToAnInvokable iReferToAnInvokable)
            {
                var name = iReferToAnInvokable.InvokableDescription.Name;
                // if the node isn't actually the entry point, add an action to go to the entrypoint
                if (!(node is EntryPoint))
                {
                    actions = actions.Append(
                        new QuickAction($"Go to definition of {name}",
                            () => Open(_currentProject.FindDefiningGraph(iReferToAnInvokable.InvokableDescription))
                        )
                    );
                }

                actions = actions.Append(
                    new QuickAction($"Refactor {name}",
                        () => _invokableRefactorDialog.Open(iReferToAnInvokable.InvokableDescription)
                    )
                );
            }

            _quickActionsPopup.Open(position, actions);
        }

        private void OnOpenFilePressed()
        {
            _fileDialog.Mode = FileDialog.ModeEnum.OpenFile;
            _fileDialog.PopupCentered();
            _fileDialog.Connect("file_selected")
                .WithFlags(ConnectFlags.Oneshot)
                .To(this, nameof(OnOpenFile));
        }

        private void OnOpenFile(string filename)
        {
            var file = new File();
            if (!file.FileExists(filename))
            {
                return;
            }

            var savedProject = ResourceLoader.Load<SavedProject>(filename, "", noCache: true);
            if (savedProject == null)
            {
                GD.Print("Cannot load file!");
                return;
            }

            Clear();

            _currentFile = filename;
            _fileNameLabel.Text = filename;

            _currentProject = new ScadProject(_rootResolver);
            _currentProject.Load(savedProject, _currentFile);

            Open(_currentProject.MainModule);
            RefreshControls();

            RenderScadOutput();
        }

        private void OnSaveAsPressed()
        {
            _fileDialog.Mode = FileDialog.ModeEnum.SaveFile;
            _fileDialog.PopupCentered();
            _fileDialog.Connect("file_selected")
                .WithFlags(ConnectFlags.Oneshot)
                .To(this, nameof(OnSaveFileSelected));
        }

        private void OnSaveFileSelected(string filename)
        {
            _currentFile = filename;
            _fileNameLabel.Text = filename;
            MarkDirty(true);
        }


        private void OnPreviewToggled(bool preview)
        {
            _editingInterface.Visible = !preview;
            _textEdit.Visible = preview;
        }


        private void MarkDirty(bool codeChange)
        {
            _dirty = true;
            _codeChanged = codeChange;
            if (_codeChanged)
            {
                _forceSaveButton.Text = "[.!.]";
            }
            else
            {
                _forceSaveButton.Text = "[...]";
            }
        }

        private void SaveChanges()
        {
            if (!_dirty)
            {
                return;
            }

            if (_codeChanged)
            {
                RenderScadOutput();
            }

            if (_currentFile != null)
            {
                // save resource
                var savedProject = _currentProject.Save();
                if (ResourceSaver.Save(_currentFile, savedProject) != Error.Ok)
                {
                    GD.Print("Cannot save project!");
                }
                else
                {
                    GD.Print("Saved project!");
                }
            }

            _forceSaveButton.Text = "[OK]";
            _dirty = false;
        }

        private void RenderScadOutput()
        {
            var rendered = _currentProject.Render();
            _textEdit.Text = rendered;

            if (_currentFile != null)
            {
                // save rendered output
                var file = new File();
                if (file.Open(_currentFile + ".scad", File.ModeFlags.Write) == Error.Ok)
                {
                    file.StoreString(rendered);
                    file.Close();
                }
                else
                {
                    GD.Print("Cannot save SCAD!");
                }
            }
        }
    }


    class NodeBasedEntry : IAddDialogEntry
    {
        private readonly ScadNode _template;
        private readonly Func<ScadNode> _factory;
        private readonly Action<RequestContext, NodeBasedEntry> _action;

        public string Title => _template.NodeTitle;
        public string Keywords => _template.NodeDescription;
        public Action<RequestContext> Action => Select;

        private void Select(RequestContext obj)
        {
            _action(obj, this);
        }

        public Texture Icon { get; }

        public ScadNode CreateNode() => _factory();

        public NodeBasedEntry(Texture icon, Func<ScadNode> factory, Action<RequestContext, NodeBasedEntry> action)
        {
            _factory = factory;
            _template = _factory();
            _action = action;
            Icon = icon;
        }

        // TODO: we need a distinction here whether the node simply doesn't fit context
        // or if it is actually not allowed to be used here. E.g. right now we can use
        // module calls in functions which we should not be able to do.
        public bool Matches(RequestContext context)
        {
            // if this came from a node left of us, check if we have a matching input port
            if (context.SourceNode != null)
            {
                for (var i = 0; i < _template.InputPortCount; i++)
                {
                    var connection = new ScadConnection(context.Source, context.SourceNode, context.LastPort, _template,
                        i);
                    if (ConnectionRules.CanConnect(connection).Decision == ConnectionRules.OperationRuleDecision.Allow)
                    {
                        return true;
                    }
                }
            }

            // if this came from a node right of us, check if we have a matching output port
            if (context.DestinationNode != null)
            {
                for (var i = 0; i < _template.OutputPortCount; i++)
                {
                    var connection = new ScadConnection(context.Source, _template, i, context.DestinationNode,
                        context.LastPort);
                    if (ConnectionRules.CanConnect(connection).Decision == ConnectionRules.OperationRuleDecision.Allow)
                    {
                        return true;
                    }
                }
            }

            // otherwise it doesn't match
            return false;
        }
    }
}