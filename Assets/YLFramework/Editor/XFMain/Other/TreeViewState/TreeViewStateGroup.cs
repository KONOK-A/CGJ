using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

#if UNITY_6000_2_OR_NEWER 
using TreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
#endif


namespace YLFramework.Core.Editor
{
    [Serializable]
    public class TreeViewStateInfo 
    {
        public int id;


        public TreeViewState treeViewState;

        public TreeViewStateInfo(int id,TreeViewState treeViewState)
        {
            this.id = id;
            this.treeViewState = treeViewState;
        } 
    }
     
    [Serializable]
    public class TreeViewStateGroup  
    {

        [SerializeField]
        private List<TreeViewStateInfo> treeViewStates = new List<TreeViewStateInfo>();

        public TreeViewState Get(int hash_code)
        {

            foreach (var item in treeViewStates)
            {
                if (item.id == hash_code) 
                    return item.treeViewState;
            }

            TreeViewState treeViewState = new TreeViewState();

            treeViewStates.Add(new TreeViewStateInfo(hash_code,treeViewState)); 
            return treeViewState;
        }

    }

}
