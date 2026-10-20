using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace YLFramework.StateMachine
{
    [System.Serializable]
    public class FSMStateScriptInfo
    {
        public string className;
        public string guid;
    }


    [System.Serializable]
    public class FSMStateNodeData
    {

#if UNITY_EDITOR
        public Rect rect;

        public void AddStateScript(UnityEngine.Object script) => FSMEditorHooks.AddScript?.Invoke(this, script);
        public void RefreshStateScripts(RuntimeFSMController controller) => FSMEditorHooks.RefreshNode?.Invoke(this, controller);
        public void RemoveStateScript(UnityEngine.Object script) => FSMEditorHooks.RemoveScript?.Invoke(this, script);

#endif

        /// <summary>
        /// 是否是默认状态
        /// </summary>
        public bool defaultState;
        /// <summary>
        /// 名称
        /// </summary>
        public string name;

        /// <summary>
        /// 状态脚本的名称
        /// </summary>
        public List<FSMStateScriptInfo> StateScripts = new List<FSMStateScriptInfo>();

        /// <summary>
        /// 当前状态的父节点名称
        /// </summary>
        public List<string> parents;

        /// <summary>
        /// 是否为子状态机
        /// </summary>
        public bool isSubStateMachine = false;

        /// <summary>
        /// 是否为内置状态
        /// </summary>
        public bool isBuildInState = false;

        /// <summary>
        /// 内置状态名称
        /// </summary>
        public string buildInStateName = null;

        #region 属性

        /// <summary>
        /// 判断当前状态是否为Any状态
        /// </summary>
        public bool IsAnyState
        {
            get
            {
                if (name == FSMConst.anyState)
                    return true;

                if (isBuildInState && buildInStateName == FSMConst.anyState)
                    return true;

                return false;
            }
        }

        /// <summary>
        /// 判断当前状态是否Entry状态
        /// </summary>
        public bool IsEntryState
        {
            get {

                if (name == FSMConst.entryState)
                    return true;

                if (isBuildInState && buildInStateName == FSMConst.entryState)
                    return true;

                return false;
            }
        }

        /// <summary>
        /// 判断是否为返回上一层的按钮
        /// </summary>
        public bool IsUpState
        {
            get {

                if (isBuildInState && buildInStateName == FSMConst.up)
                    return true;

                return false;
            }
        }

        public string DisplayName
        {
            get
            {
                if (IsAnyState)
                    return FSMConst.anyState;
                if (IsEntryState)
                    return FSMConst.entryState;
                if (IsUpState)
                {
                    if (parents != null && parents.Count > 1)
                        return string.Format("(Up){0}", parents[parents.Count - 2]);
                    else
                        return "(Up)Base Layer";
                }

                return name;
            }
        }

        public string ParentPath
        {
            get
            {
                if (parents == null || parents.Count == 0) return string.Empty;

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < parents.Count; i++) {
                    sb.Append(parents[i]);
                    if (i < parents.Count - 1)
                        sb.Append("/");
                }

                return sb.ToString();
            }
        }

        public string NextParentPath {
            get
            {
                List<string> next_parents = new List<string>();

                if (parents != null && parents.Count != 0)
                    next_parents.AddRange(parents);

                next_parents.Add(name);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < next_parents.Count; i++)
                {
                    sb.Append(next_parents[i]);
                    if (i < next_parents.Count - 1)
                        sb.Append("/");
                }

                return sb.ToString();
            }
        }

        public string Parent
        {
            get {
                if (parents == null || parents.Count == 0) return string.Empty;

                return parents[parents.Count - 1];
            }
        }

        #endregion

        public bool CompareParent(FSMStateNodeData node)
        {
            if (BaseLayer() && node.BaseLayer()) return true;

            if(parents == null || node.parents == null) return false;

            if (node.parents.Count != this.parents.Count) return false;

            for (int i = 0; i < parents.Count; i++)
            {
                if (!parents[i].Equals(node.parents[i]))
                    return false;
            }

            return true;
        }

        public bool CompareParent(List<string> parents) {

            if (BaseLayer()) return parents == null || parents.Count == 0;

            if (parents == null || this.parents == null) return false;

            if (parents.Count != this.parents.Count) return false;

            for (int i = 0; i < this.parents.Count; i++)
            {
                if (!this.parents[i].Equals(parents[i]))
                    return false;
            }

            return true;
        }

        public bool BaseLayer()
        {
            return this.parents == null || this.parents.Count == 0;
        }


        public bool ContainsParent(string parent)
        {
            if(BaseLayer()) return false;
            return parents.Contains(parent);
        }

        public bool BelongToParent(string parent)
        {
            if(BaseLayer()) return false;
            return parents[parents.Count - 1].Equals(parent);
        }

        public void RenameParent(string oldParent, string newName) {
            int index =  parents.IndexOf(oldParent);
            parents[index] = newName;
        }

        public List<string> GetParent() {
            List<string> parents = new List<string>();
            if (this.parents == null) return parents;
            parents.AddRange(this.parents);
            return parents;
        }

    }

}
