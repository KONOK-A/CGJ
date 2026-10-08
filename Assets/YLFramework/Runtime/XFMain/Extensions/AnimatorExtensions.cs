// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/AnimatorExtensions.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Core.Extensions
{
     
    public static class AnimatorExtensions
    {
        /// <summary>
        /// 同步动画,把另外一个的Animator的数据同步当前的Animator
        /// </summary>
        /// <param name="animator"></param>
        /// <param name="other"></param>
        public static void Sync(this Animator animator, Animator other)
        {
            // 同步参数
            foreach (var parameter in other.parameters)
            {
                int hash = parameter.nameHash;
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        animator.SetFloat(hash, other.GetFloat(hash));
                        break;
                    case AnimatorControllerParameterType.Int:
                        animator.SetInteger(hash, other.GetInteger(hash));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        animator.SetBool(hash, other.GetBool(hash));
                        break;
                }
            }
            // 同步正在播放的动画
            int layerCount = animator.layerCount;
            for (int i = 0; i < layerCount; i++)
            {
                animator.SetLayerWeight(i, other.GetLayerWeight(i));
                AnimatorStateInfo info = other.GetCurrentAnimatorStateInfo(i);
                animator.Play(info.fullPathHash, i, info.normalizedTime);
            }
            // 更新
            animator.Update(Time.deltaTime);
        }
    }
}