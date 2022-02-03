using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.GraphToolsFoundation.CommandStateObserver;
using UnityEngine;
using UnityEngine.GraphToolsFoundation.Overdrive;

namespace UnityEditor.GraphToolsFoundation.Overdrive
{
    /// <summary>
    /// Command to create a node from a <see cref="GraphNodeModelSearcherItem"/>.
    /// </summary>
    public class CreateNodeCommand : UndoableCommand
    {
        /// <summary>
        /// Data used by <see cref="CreateNodeCommand"/> to create one node.
        /// </summary>
        public struct NodeData
        {
            /// <summary>
            /// The position where to create the node.
            /// </summary>
            public Vector2 Position;

            /// <summary>
            /// The edge model on which to insert the newly created node.
            /// </summary>
            public IEdgeModel EdgeModel;

            /// <summary>
            /// The port to which to connect the new node.
            /// </summary>
            public IPortModel PortModel;

            /// <summary>
            /// The variable for which to create nodes.
            /// </summary>
            public IVariableDeclarationModel VariableDeclaration;

            /// <summary>
            /// The searcher item representing the node to create.
            /// </summary>
            public GraphNodeModelSearcherItem SearcherItem;

            /// <summary>
            /// True if the new node should be aligned to the connected port.
            /// </summary>
            public bool AutoAlign;

            /// <summary>
            /// The SerializableGUID to assign to the newly created item.
            /// </summary>
            public SerializableGUID Guid;
        }

        /// <summary>
        /// Data for all the nodes the command should create.
        /// </summary>
        public List<NodeData> CreationData;

        /// <summary>
        /// Initializes a new CreateNodeFromSearcherCommand.
        /// </summary>
        public CreateNodeCommand()
        {
            UndoString = "Create Node";
            CreationData = new List<NodeData>();
        }

        /// <summary>
        /// Initializes a new <see cref="CreateNodeCommand"/> to create a node on the graph.
        /// </summary>
        /// <param name="item">The searcher item representing the node to create.</param>
        /// <param name="position">The position where to create the node.</param>
        /// <param name="guid">The SerializableGUID to assign to the newly created item. If none is provided, a new
        /// SerializableGUID will be generated for it.</param>
        /// <returns>A <see cref="CreateNodeCommand"/> that can be dispatched to create a node on the graph.</returns>
        public static CreateNodeCommand OnGraph(GraphNodeModelSearcherItem item,
            Vector2 position = default,
            SerializableGUID guid = default)
        {
            return new CreateNodeCommand().WithNodeOnGraph(item, position, guid);
        }

        /// <summary>
        /// Initializes a new <see cref="CreateNodeCommand"/> to create a variable on the graph.
        /// </summary>
        /// <param name="model">The declaration for the variable to create on the graph.</param>
        /// <param name="position">The position where to create the node.</param>
        /// <param name="guid">The SerializableGUID to assign to the newly created item. If none is provided, a new
        /// SerializableGUID will be generated for it.</param>
        /// <returns>A <see cref="CreateNodeCommand"/> that can be dispatched to create a variable on the graph.</returns>
        public static CreateNodeCommand OnGraph(IVariableDeclarationModel model,
            Vector2 position = default,
            SerializableGUID guid = default)
        {
            return new CreateNodeCommand().WithNodeOnGraph(model, position, guid);
        }

        /// <summary>
        /// Initializes a new <see cref="CreateNodeCommand"/> to insert a node on an existing edge.
        /// </summary>
        /// <param name="item">The searcher item representing the node to create.</param>
        /// <param name="edgeModel">The edge to insert the new node one.</param>
        /// <param name="position">The position where to create the node.</param>
        /// <param name="guid">The SerializableGUID to assign to the newly created item. If none is provided, a new
        /// SerializableGUID will be generated for it.</param>
        /// <returns>A <see cref="CreateNodeCommand"/> that can be dispatched to create a node on an edge.</returns>
        public static CreateNodeCommand OnEdge(GraphNodeModelSearcherItem item,
            IEdgeModel edgeModel,
            Vector2 position = default,
            SerializableGUID guid = default)
        {
            return new CreateNodeCommand().WithNodeOnEdge(item, edgeModel, position, guid);
        }

        /// <summary>
        /// Initializes a new <see cref="CreateNodeCommand"/> to create a node and connect it to an existing port.
        /// </summary>
        /// <param name="item">The searcher item representing the node to create.</param>
        /// <param name="portModel">The port to connect the new node to.</param>
        /// <param name="position">The position where to create the node.</param>
        /// <param name="autoAlign">If true, the created node will be automatically aligned after being created.</param>
        /// <param name="guid">The SerializableGUID to assign to the newly created item. If none is provided, a new
        /// SerializableGUID will be generated for it.</param>
        /// <returns>A <see cref="CreateNodeCommand"/> that can be dispatched to create a node on a port.</returns>
        public static CreateNodeCommand OnPort(GraphNodeModelSearcherItem item,
            IPortModel portModel,
            Vector2 position = default,
            bool autoAlign = false,
            SerializableGUID guid = default)
        {
            return new CreateNodeCommand().WithNodeOnPort(item, portModel, position, autoAlign, guid);
        }

        /// <summary>
        /// Initializes a new <see cref="CreateNodeCommand"/> to create a variable and connect it to an existing port.
        /// </summary>
        /// <param name="model">The declaration for the variable to create on the graph.</param>
        /// <param name="portModel">The port to connect the new node to.</param>
        /// <param name="position">The position where to create the node.</param>
        /// /// <param name="autoAlign">If true, the created node will be automatically aligned after being created.</param>
        /// <param name="guid">The SerializableGUID to assign to the newly created item. If none is provided, a new
        /// SerializableGUID will be generated for it.</param>
        /// <returns>A <see cref="CreateNodeCommand"/> that can be dispatched to create a node on an edge.</returns>
        public static CreateNodeCommand OnPort(IVariableDeclarationModel model,
            IPortModel portModel,
            Vector2 position = default,
            bool autoAlign = false,
            SerializableGUID guid = default)
        {
            return new CreateNodeCommand().WithNodeOnPort(model, portModel, position, autoAlign, guid);
        }

        /// <summary>
        /// Initializes a new CreateNodeFromSearcherCommand.
        /// </summary>
        /// <param name="position">The position where to create the node.</param>
        /// <param name="selectedItem">The searcher item representing the node to create.</param>
        /// <param name="guid">The SerializableGUID to assign to the newly created item. If none is provided, a new
        /// SerializableGUID will be generated for it.</param>
        [Obsolete("Use CreateNodeCommand.OnGraph() or new CreateNodeCommand().WithNodeOnGraph() instead.")]
        public CreateNodeCommand(Vector2 position,
                                             GraphNodeModelSearcherItem selectedItem,
                                             SerializableGUID guid = default) : this()
        {
            this.WithNodeOnGraph(selectedItem, position, guid);
        }

        /// <summary>
        /// Default command handler for CreateNodeFromSearcherCommand.
        /// </summary>
        /// <param name="undoState">The undo state component.</param>
        /// <param name="graphViewState">The graph view state component.</param>
        /// <param name="preferences">The tool preferences.</param>
        /// <param name="command">The command to handle.</param>
        public static void DefaultCommandHandler(UndoStateComponent undoState, GraphViewStateComponent graphViewState,
            Preferences preferences, CreateNodeCommand command)
        {
            if (command.CreationData.Count <= 0)
            {
                Debug.LogError("Creation command dispatched with 0 item to create");
                return;
            }

            var graphModel = graphViewState.GraphModel;
            var stencil = graphModel.Stencil as Stencil;

            var variablesToAdd = command.CreationData.Where(nodeData => nodeData.VariableDeclaration != null)
                .Where(v => stencil == null || stencil.CanCreateVariableInGraph(v.VariableDeclaration, graphModel))
                .ToList();

            if (!variablesToAdd.Any() && command.CreationData.All(nodeData => nodeData.SearcherItem == null))
                return;

            using (var undoStateUpdater = undoState.UpdateScope)
            {
                undoStateUpdater.SaveSingleState(graphViewState, command);
            }

            using (var graphUpdater = graphViewState.UpdateScope)
            {
                foreach (var creationData in command.CreationData)
                {
                    if ((creationData.VariableDeclaration == null) == (creationData.SearcherItem == null))
                    {
                        Debug.LogWarning("Creation command dispatched with invalid item to create: either provide VariableDeclaration or SearcherItem. Ignoring this item.");
                        continue;
                    }

                    if (creationData.PortModel != null && creationData.EdgeModel != null)
                    {
                        Debug.LogError(
                            "Creation command dispatched with invalid item to create: Can't create an item on both PortModel and EdgeModel. Ignoring this item.");
                        continue;
                    }

                    var guid = creationData.Guid.Valid ? creationData.Guid : SerializableGUID.Generate();

                    // Delete previous connections
                    var existingPortToConnect = creationData.PortModel;
                    if (existingPortToConnect != null && existingPortToConnect.Capacity != PortCapacity.Multi)
                    {
                        var existingEdges = existingPortToConnect.GetConnectedEdges().ToList();
                        if (existingEdges.Any())
                        {
                            graphViewState.GraphModel.DeleteEdges(existingEdges);
                            graphUpdater.MarkDeleted(existingEdges);
                        }
                    }

                    // Create new element
                    IGraphElementModel createdElement;
                    if (creationData.VariableDeclaration != null)
                    {
                        createdElement = graphModel.CreateVariableNode(creationData.VariableDeclaration, creationData.Position, guid);
                    }
                    else
                    {
                        var elementModels = creationData.SearcherItem.CreateElements.Invoke(
                            new GraphNodeCreationData(graphModel, creationData.Position, guid: guid));
                        createdElement = elementModels.FirstOrDefault();
                    }

                    if (createdElement != null)
                        graphUpdater.MarkNew(createdElement);

                    // Connect created element to existing port
                    if (existingPortToConnect != null)
                    {
                        if (createdElement is IPortNodeModel newModelToConnect)
                        {
                            var newPortToConnect = newModelToConnect.GetPortFitToConnectTo(existingPortToConnect);

                            if (newPortToConnect != null)
                            {
                                IEdgeModel newEdge;
                                if (existingPortToConnect.Direction == PortDirection.Output)
                                {
                                    if ((existingPortToConnect.NodeModel is IConstantNodeModel &&
                                            preferences.GetBool(BoolPref.AutoItemizeConstants)) ||
                                        (existingPortToConnect.NodeModel is IVariableNodeModel &&
                                            preferences.GetBool(BoolPref.AutoItemizeVariables)))
                                    {
                                        var newNode = graphModel.CreateItemizedNode(EdgeCommandConfig.nodeOffset,
                                            ref existingPortToConnect);
                                        graphUpdater.MarkNew(newNode);
                                    }

                                    newEdge = graphModel.CreateEdge(newPortToConnect, existingPortToConnect);
                                }
                                else
                                {
                                    newEdge = graphModel.CreateEdge(existingPortToConnect, newPortToConnect);
                                }
                                graphUpdater.MarkNew(newEdge);

                                if (newEdge != null && creationData.AutoAlign ||
                                    preferences.GetBool(BoolPref.AutoAlignDraggedEdges))
                                {
                                    graphUpdater.MarkModelToAutoAlign(newEdge);
                                }
                            }
                        }
                    }
                    // insert created element on existing edge
                    else if (creationData.EdgeModel != null)
                    {
                        if (createdElement is IInputOutputPortsNodeModel newModelToConnect)
                        {
                            var edgeInput = creationData.EdgeModel.ToPort;
                            var edgeOutput = creationData.EdgeModel.FromPort;

                            // Delete old edge
                            var deletedModels = graphModel.DeleteEdge(creationData.EdgeModel);
                            graphUpdater.MarkDeleted(deletedModels);

                            // Connect input port
                            var inputPortModel =
                                newModelToConnect.InputsByDisplayOrder.FirstOrDefault(p =>
                                    p?.PortType == edgeOutput?.PortType);

                            if (inputPortModel != null)
                            {
                                var newEdge = graphModel.CreateEdge(inputPortModel, edgeOutput);
                                graphUpdater.MarkNew(newEdge);
                            }

                            // Connect output port
                            var outputPortModel = newModelToConnect.GetPortFitToConnectTo(edgeInput);

                            if (outputPortModel != null)
                            {
                                var newEdge = graphModel.CreateEdge(edgeInput, outputPortModel);
                                graphUpdater.MarkNew(newEdge);
                            }
                        }
                    }
                }
            }
        }
    }


    /// <summary>
    /// Command to create a node from a <see cref="GraphNodeModelSearcherItem"/> and to connect it to existing ports.
    /// </summary>
    [Obsolete("Use CreateNodeCommand.OnPort() or new CreateNodeCommand().WithNodeOnPort() instead.")]
    public class CreateNodeFromPortCommand : UndoableCommand
    {
        /// <summary>
        /// The ports to which to connect the new node.
        /// </summary>
        public IReadOnlyList<IPortModel> PortModels;
        /// <summary>
        /// The position where to create the node.
        /// </summary>
        public Vector2 Position;
        /// <summary>
        /// The searcher item representing the node to create.
        /// </summary>
        public GraphNodeModelSearcherItem SelectedItem;
        /// <summary>
        /// Edges to delete.
        /// </summary>
        public IReadOnlyList<IEdgeModel> EdgesToDelete;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateNodeFromPortCommand"/> class.
        /// </summary>
        public CreateNodeFromPortCommand()
        {
            UndoString = "Create Node";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateNodeFromPortCommand"/> class.
        /// </summary>
        /// <param name="portModel">The ports to which to connect the new node.</param>
        /// <param name="position">The position where to create the node.</param>
        /// <param name="selectedItem">The searcher item representing the node to create.</param>
        /// <param name="edgesToDelete">Edges to delete.</param>
        public CreateNodeFromPortCommand(IReadOnlyList<IPortModel> portModel, Vector2 position, GraphNodeModelSearcherItem selectedItem,
                                         IReadOnlyList<IEdgeModel> edgesToDelete = null) : this()
        {
            PortModels = portModel;
            Position = position;
            SelectedItem = selectedItem;
            EdgesToDelete = edgesToDelete;
        }

        /// <summary>
        /// Default command handler.
        /// </summary>
        /// <param name="undoState">The undo state component.</param>
        /// <param name="graphViewState">The graph view state component.</param>
        /// <param name="preferences">The tool preferences.</param>
        /// <param name="command">The command.</param>
        public static void DefaultCommandHandler(UndoStateComponent undoState, GraphViewStateComponent graphViewState, Preferences preferences, CreateNodeFromPortCommand command)
        {
            using (var undoStateUpdater = undoState.UpdateScope)
            {
                undoStateUpdater.SaveSingleState(graphViewState, command);
            }

            using (var graphUpdater = graphViewState.UpdateScope)
            {
                var graphModel = graphViewState.GraphModel;

                var position = command.Position - Vector2.up * EdgeCommandConfig.nodeOffset;
                var elementModels = command.SelectedItem.CreateElements.Invoke(
                    new GraphNodeCreationData(graphModel, position));

                graphUpdater.MarkNew(elementModels);

                if (!elementModels.Any() || !(elementModels[0] is IPortNodeModel selectedNodeModel))
                    return;

                var otherPortModel = selectedNodeModel.GetPortFitToConnectTo(command.PortModels.First());

                if (otherPortModel == null)
                    return;

                foreach (var portModel in command.PortModels)
                {
                    var thisPortModel = portModel;
                    IEdgeModel newEdge;
                    if (thisPortModel.Direction == PortDirection.Output)
                    {
                        if ((thisPortModel.NodeModel is IConstantNodeModel && preferences.GetBool(BoolPref.AutoItemizeConstants)) ||
                            (thisPortModel.NodeModel is IVariableNodeModel && preferences.GetBool(BoolPref.AutoItemizeVariables)))
                        {
                            var newNode = graphModel.CreateItemizedNode(EdgeCommandConfig.nodeOffset, ref thisPortModel);
                            graphUpdater.MarkNew(newNode);
                        }

                        newEdge = graphModel.CreateEdge(otherPortModel, thisPortModel);
                        graphUpdater.MarkNew(newEdge);
                    }
                    else
                    {
                        newEdge = graphModel.CreateEdge(thisPortModel, otherPortModel);
                        graphUpdater.MarkNew(newEdge);
                    }

                    var edgesToDelete = new List<IEdgeModel>();
                    if (command.EdgesToDelete != null)
                    {
                        edgesToDelete.AddRange(command.EdgesToDelete);
                    }

                    if (thisPortModel.Capacity == PortCapacity.Single)
                    {
                        edgesToDelete.AddRange(thisPortModel.GetConnectedEdges().Where(edgeToDelete => !edgeToDelete.Equals(newEdge)));
                    }

                    if (edgesToDelete.Any())
                    {
                        var deletedModels = graphModel.DeleteEdges(edgesToDelete);
                        graphUpdater.MarkDeleted(deletedModels);
                    }

                    if (newEdge != null && preferences.GetBool(BoolPref.AutoAlignDraggedEdges))
                    {
                        graphUpdater.MarkModelToAutoAlign(newEdge);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Command to create a node from a <see cref="GraphNodeModelSearcherItem"/> and insert in on an edge.
    /// </summary>
    [Obsolete("Use CreateNodeCommand.OnEdge() or new CreateNodeCommand().WithNodeOnEdge() instead.")]
    public class CreateNodeOnEdgeCommand : UndoableCommand
    {
        /// <summary>
        /// The edge model on which to insert the newly created node.
        /// </summary>
        public IEdgeModel EdgeModel;

        /// <summary>
        /// The position where to create the node.
        /// </summary>
        public Vector2 Position;

        /// <summary>
        /// The searcher item representing the node to create.
        /// </summary>
        public GraphNodeModelSearcherItem SelectedItem;

        /// <summary>
        /// The SerializableGUID to assign to the newly created item.
        /// </summary>
        public SerializableGUID Guid;

        /// <summary>
        /// Initializes a new CreateNodeFromSearcherCommand.
        /// </summary>
        public CreateNodeOnEdgeCommand()
        {
            UndoString = "Create Node";
        }

        /// <summary>
        /// Initializes a new CreateNodeFromSearcherCommand.
        /// </summary>
        /// <param name="edgeModel">The edge model on which to insert the newly created node.</param>
        /// <param name="position">The position where to create the node.</param>
        /// <param name="selectedItem">The searcher item representing the node to create.</param>
        /// <param name="guid">The SerializableGUID to assign to the newly created item. If none is provided, a new
        /// SerializableGUID will be generated for it.</param>
        public CreateNodeOnEdgeCommand(IEdgeModel edgeModel, Vector2 position,
                                       GraphNodeModelSearcherItem selectedItem, SerializableGUID guid = default) : this()
        {
            EdgeModel = edgeModel;
            Position = position;
            SelectedItem = selectedItem;
            Guid = guid.Valid ? guid : SerializableGUID.Generate();
        }

        /// <summary>
        /// Default command handler for CreateNodeOnEdgeCommand.
        /// </summary>
        /// <param name="undoState">The undo state component.</param>
        /// <param name="graphViewState">The graph view state component.</param>
        /// <param name="command">The command to handle.</param>
        public static void DefaultCommandHandler(UndoStateComponent undoState, GraphViewStateComponent graphViewState, CreateNodeOnEdgeCommand command)
        {
            using (var undoStateUpdater = undoState.UpdateScope)
            {
                undoStateUpdater.SaveSingleState(graphViewState, command);
            }

            using (var graphUpdater = graphViewState.UpdateScope)
            {
                var edgeInput = command.EdgeModel.ToPort;
                var edgeOutput = command.EdgeModel.FromPort;

                // Instantiate node
                var graphModel = graphViewState.GraphModel;

                var position = command.Position - Vector2.up * EdgeCommandConfig.nodeOffset;

                var elementModels = command.SelectedItem.CreateElements.Invoke(
                    new GraphNodeCreationData(graphModel, position, guid: command.Guid));

                graphUpdater.MarkNew(elementModels);

                if (elementModels.Length == 0 || !(elementModels[0] is IInputOutputPortsNodeModel selectedNodeModel))
                    return;

                // Delete old edge
                var deletedModels = graphModel.DeleteEdge(command.EdgeModel);
                graphUpdater.MarkDeleted(deletedModels);

                // Connect input port
                var inputPortModel = selectedNodeModel.InputsByDisplayOrder.FirstOrDefault(p => p?.PortType == edgeOutput?.PortType);

                if (inputPortModel != null)
                {
                    var newEdge = graphModel.CreateEdge(inputPortModel, edgeOutput);
                    graphUpdater.MarkNew(newEdge);
                }

                // Find first matching output type and connect it
                var outputPortModel = selectedNodeModel.GetPortFitToConnectTo(edgeInput);

                if (outputPortModel != null)
                {
                    var newEdge = graphModel.CreateEdge(edgeInput, outputPortModel);
                    graphUpdater.MarkNew(newEdge);
                }
            }
        }
    }
}
