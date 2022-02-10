using System;
using System.Collections.Generic;
using UnityEditor.GraphToolsFoundation.Overdrive;
using UnityEditor.GraphToolsFoundation.Searcher;
using UnityEditor.ShaderGraph.Registry;

namespace UnityEditor.ShaderGraph.GraphUI
{
    /// <summary>
    /// A RegistryNodeSearcherItem is a GraphNodeModelSearcherItem associated with a registry key. The key is exposed
    /// to make filtering easier.
    /// </summary>
    public class RegistryNodeSearcherItem : GraphNodeModelSearcherItem
    {
        public readonly RegistryKey registryKey;

        public RegistryNodeSearcherItem(
            IGraphModel graphModel,
            RegistryKey registryKey,
            string name,
            ISearcherItemData data = null,
            List<SearcherItem> children = null,
            Func<string> getName = null,
            string help = null
        ) : base(graphModel, data,  creationData => graphModel.CreateGraphDataNode(registryKey, name, creationData.Position, creationData.Guid, creationData.SpawnFlags), name, children, getName, help)
        {

            // Func<IGraphNodeCreationData, IGraphElementModel> createElement,
            this.registryKey = registryKey;
        }
    }
}
