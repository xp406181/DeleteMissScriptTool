/* 
Author:Wang Zhe
Date:2018-4-1
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Threading;


namespace WZTool
{
    public class DeleteMissScriptTool : Editor
    {
        public delegate bool CheckHandler(string matchStr);
        public const string MonobehaviorPattern = @"--- !u!114 &(\d+)[\s\S]+?m_Name";
        public const string MissScriptFlagPattern = @"m_Script: {fileID: 0}";
        public const string ScriptFileIDAndGuidPattern = @"--- !u!114 &(\d{8})[\s\S]+?m_Script: {fileID: (?:11500000[\s\S]+?([a-f0-9]{32})|(0))";
        public const string ScriptFileIDPattern = @"  - 114: {fileID: (\d+)}\n";
        public const string MonobehaviorOriginStr = @"--- !u!114 &{0}[\s\S]+?" + MonobehaviorReplaceStr;
        public const string MonobehaviorReplaceStr = @"---";


        [MenuItem("Window/DeleteMissScriptTool")]//在unity菜单Window下有MyWindow选项
        static void Init()
        {
            DeleteMissScriptWindow myWindow = (DeleteMissScriptWindow)EditorWindow.GetWindow(typeof(DeleteMissScriptWindow), false, "删除miss脚本", true);//创建窗口
            myWindow.Show();//展示
        }

        [MenuItem("Assets/RightClick/PrintAllPrefabs")]
        public static void Func1()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets/Resources/prefab" });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Debug.Log(path);
            }
        }

        [MenuItem("Assets/RightClick/TestDeleteMissScript")]
        public static void Func2()
        {
            var prefabPath = "F:/WorkFolder/master/client/GameClient_UI/Assets/Resources/prefab/UIItems/Lab/ui_item_tech.prefab";
            var scriptFileID = "11418506";
            DeleteMissScriptTool.DeleteMissScript(prefabPath, scriptFileID);
        }

        [MenuItem("Assets/RightClick/TestRegex")]
        public static void Func3()
        {
            var prefabPath = GetPathByGUID(Selection.assetGUIDs[0]);
            var fileID2guid = GetScriptFileIDAndGuid(prefabPath);

            foreach (var item in fileID2guid)
            {
                Debug.LogFormat("fileID:{0},guid:{1}", item.Key, item.Value);
            }
        }

        /// <summary>
        /// guid转换为全路径
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static string GetPathByGUID(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefabPath = Application.dataPath + path.Substring("Assets".Length);
            return prefabPath;
        }

        /// <summary>
        /// 读取文件流
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetPrefabString(string path)
        {
            using (StreamReader sr = File.OpenText(path))
            {
                return sr.ReadToEnd();
            }
        }

        /// <summary>
        /// 通过guid读取文件流
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static string GetPrefabStringByGUID(string guid)
        {
            var prefabPath = GetPathByGUID(guid);
            var allText = GetPrefabString(prefabPath);
            return allText;
        }

        /// <summary>
        /// 写文件流
        /// </summary>
        /// <param name="path"></param>
        /// <param name="str"></param>
        private static void WritePrefabString(string path, string str)
        {
            using (StreamWriter sw = new StreamWriter(path))
            {
                sw.Write(str);
                sw.Flush();
            }
        }

        /// <summary>
        /// 判断指定节点是否含有空脚本
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static bool CheckMissComponentObj(GameObject node)
        {
            var components = node.GetComponentsInChildren<Component>();
            var ret = false;
            for (int j = 0; j < components.Length; j++)
            {
                if (components[j] == null)
                {
                    ret = true;
                    break;
                }
            }
            return ret;
        }


        /// <summary>
        /// 获取指定预制体上没有guid的脚本的fileID
        /// </summary>
        /// <param name="prefabPath">预制体全路径</param>
        /// <returns></returns>
        public static List<string> GetScriptNoGUIDFileIDLst(string prefabPath)
        {
            string pattern1 = MonobehaviorPattern;
            string pattern2 = MissScriptFlagPattern;
            var inputString = GetPrefabString(prefabPath);
            var ret = new List<string>();
            MatchCollection mc = Regex.Matches(inputString, pattern1);
            foreach (Match m in mc)
            {
                var tempM = Regex.Match(m.Value, pattern2);
                if (tempM.Success)
                {
                    var scriptFileID = m.Groups[1].Value;
                    ret.Add(scriptFileID);
                }
            }
            return ret;
        }

        /// <summary>
        /// 获取指定预制体上脚本的fileID到guid映射关系，只包含guid不为0的脚本
        /// </summary>
        /// <param name="prefabPath">预制体全路径</param>
        /// <param name="fileID2guid">结果</param>
        /// <summary>
        public static Dictionary<string, string> GetScriptFileIDAndGuid(string prefabPath)
        {
            var inputString = GetPrefabString(prefabPath);
            var fileID2guid = new Dictionary<string, string>();
            string pattern = ScriptFileIDAndGuidPattern;
            MatchCollection mc = Regex.Matches(inputString, pattern);
            foreach (Match m in mc)
            {
                var scriptFileID = m.Groups[1].Value;
                var scriptGUID = m.Groups[2].Value;
                if (string.IsNullOrEmpty(scriptGUID) == false)//这一步只保存guid还在的脚本fileID
                {
                    if (fileID2guid.ContainsKey(scriptFileID) == false)
                    {
                        fileID2guid.Add(scriptFileID, scriptGUID);
                    }
                    else
                    {
                        Debug.LogFormat("{0}---{1}---{2}", prefabPath, scriptGUID, scriptFileID);
                    }
                }
            }
            return fileID2guid;
        }

        /// <summary>
        /// 删除指定预制体上指定脚本
        /// </summary>
        /// <param name="prefabFullPath">预制体全路径</param>
        /// <param name="fileID">脚本的fileID</param>
        /// <returns></returns>
        public static void DeleteMissScript(string prefabFullPath, string fileID)
        {
            string inputString = GetPrefabString(prefabFullPath);

            var scriptFileIDOriginStr = @"  - 114: {fileID: " + fileID + @"}\n";
            inputString = Regex.Replace(inputString, scriptFileIDOriginStr, string.Empty);
            inputString = Regex.Replace(inputString, string.Format(MonobehaviorOriginStr, fileID), MonobehaviorReplaceStr);
            WritePrefabString(prefabFullPath, inputString);
        }
    }


    public class DeleteMissScriptWindow : EditorWindow
    {
        Vector2 scrollPos = Vector2.zero;
        List<GameObject> m_missObjs = new List<GameObject>();

        /// <summary>
        /// 搜索所有含有miss脚本的预制体
        /// </summary>
        private void SearchMissComponentObjs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets/Resources/prefab" });
            int i = 0;
            foreach (var guid in guids)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                EditorUtility.DisplayProgressBar("Processing...", "遍历丢失脚本Prefab中....", i++ / (float)guids.Length);
                var obj = (GameObject)AssetDatabase.LoadAssetAtPath(prefabPath, typeof(GameObject));

                if (DeleteMissScriptTool.CheckMissComponentObj(obj) == true)
                {
                    m_missObjs.Add(obj);
                    Debug.Log(prefabPath, obj);
                }
            }

            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// 打印没有guid的miss脚本信息
        /// </summary>
        void PrintMissScriptNoGUID()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets/Resources/prefab" });//获取所有预制体guid
            int i = 0;
            foreach (var prefabGUID in prefabGuids)
            {
                EditorUtility.DisplayProgressBar("Processing...", "打印没有guid的miss脚本中....", i++ / (float)prefabGuids.Length);
                var prefabPath = DeleteMissScriptTool.GetPathByGUID(prefabGUID);//预制体全路径
                var scriptFileIDLst = DeleteMissScriptTool.GetScriptNoGUIDFileIDLst(prefabPath);

                foreach (var fileID in scriptFileIDLst)
                {
                    Debug.LogFormat("prefab:{0},script:{1}", prefabPath, fileID);
                }
            }
            EditorUtility.ClearProgressBar();
        }



        /// <summary>
        /// 删除所有预制体上没有guid的miss脚本引用
        /// </summary>
        void DeleteMissScriptNoGUID()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets/Resources/prefab" });//获取所有预制体guid
            int i = 0;
            foreach (var prefabGUID in prefabGuids)
            {
                EditorUtility.DisplayProgressBar("Processing...", "清理没有guid的miss脚本中....", i++ / (float)prefabGuids.Length);
                var prefabPath = DeleteMissScriptTool.GetPathByGUID(prefabGUID);//预制体全路径
                var scriptFileIDLst = DeleteMissScriptTool.GetScriptNoGUIDFileIDLst(prefabPath);

                foreach (var fileID in scriptFileIDLst)
                {
                    DeleteMissScriptTool.DeleteMissScript(prefabPath, fileID);
                    Debug.LogFormat("processed prefab no guid:{0}", prefabPath);
                }
            }
            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// 获取有guid的miss脚本信息
        /// </summary>
        /// <param name="Dictionary<string"></param>
        /// <param name="guid2prefab"></param>
        /// <param name="Dictionary<string"></param>
        /// <param name="prefab2guid2fileID"></param>
        /// <returns></returns>
        HashSet<string> GetMissScriptGUID(out Dictionary<string, HashSet<string>> guid2prefab, out Dictionary<string, Dictionary<string, string>> prefab2guid2fileID)
        {
            string[] guids = AssetDatabase.FindAssets("t:Script");//所有脚本guid
            HashSet<string> existGuids = new HashSet<string>(guids);

            HashSet<string> referenceGuids = new HashSet<string>();//所有预制体引用的脚本guid
            prefab2guid2fileID = new Dictionary<string, Dictionary<string, string>>();//脚本guid到预制体列表的映射
            guid2prefab = new Dictionary<string, HashSet<string>>();//脚本guid到预制体列表的映射

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets/Resources/prefab" });//获取所有预制体guid
            int i = 0;
            foreach (var prefabGUID in prefabGuids)
            {
                EditorUtility.DisplayProgressBar("Processing...", "收集miss脚本的guid中....", i++ / (float)prefabGuids.Length);
                var prefabPath = DeleteMissScriptTool.GetPathByGUID(prefabGUID);//预制体全路径

                var fileID2guid = DeleteMissScriptTool.GetScriptFileIDAndGuid(prefabPath);//脚本guid到fileID的映射

                prefab2guid2fileID[prefabPath] = fileID2guid;  //预制体上的脚本guid到fileID的映射

                //脚本guid到预制体列表的映射
                foreach (var item in fileID2guid)
                {
                    if (guid2prefab.ContainsKey(item.Value) == false) guid2prefab[item.Value] = new HashSet<string>();
                    guid2prefab[item.Value].Add(prefabPath);
                }

                referenceGuids.UnionWith(fileID2guid.Values);//合并预制体引用的脚本guid并去重
            }
            referenceGuids.ExceptWith(guids);//提取出不存在的脚本guid
            EditorUtility.ClearProgressBar();
            return referenceGuids;
        }

        /// <summary>
        /// 打印有guid的miss脚本信息
        /// </summary>
        void PrintMissScriptWithGUID()
        {
            Dictionary<string, HashSet<string>> guid2prefab = null;
            Dictionary<string, Dictionary<string, string>> prefab2guid2fileID = null;
            var referenceGuids = GetMissScriptGUID(out guid2prefab, out prefab2guid2fileID);
            int i = 0;
            foreach (var scriptGUID in referenceGuids)
            {
                EditorUtility.DisplayProgressBar("Processing...", "打印有guid的miss脚本中....", i++ / (float)referenceGuids.Count);
                var prefabLst = guid2prefab[scriptGUID];
                foreach (var prefabPath in prefabLst)
                {
                    var fileID2guid = prefab2guid2fileID[prefabPath];
                    foreach (var item in fileID2guid)
                    {
                        if (item.Value == scriptGUID)
                        {
                            var scriptFileID = item.Key;
                            Debug.LogFormat("prefab:{0},script:{1},guid:{2}", prefabPath, scriptFileID, scriptGUID);
                        }
                    }
                }
            }

            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// 删除所有预制体上有guid的miss脚本引用
        /// </summary>
        void DeleteMissScriptWithGUID()
        {
            Dictionary<string, HashSet<string>> guid2prefab = null;
            Dictionary<string, Dictionary<string, string>> prefab2guid2fileID = null;
            var referenceGuids = GetMissScriptGUID(out guid2prefab, out prefab2guid2fileID);
            int i = 0;
            foreach (var scriptGUID in referenceGuids)
            {
                EditorUtility.DisplayProgressBar("Processing...", "清理有guid的miss脚本中....", i++ / (float)referenceGuids.Count);
                var prefabLst = guid2prefab[scriptGUID];
                foreach (var prefabPath in prefabLst)
                {
                    var fileID2guid = prefab2guid2fileID[prefabPath];
                    foreach (var item in fileID2guid)
                    {
                        if (item.Value == scriptGUID)
                        {
                            var scriptFileID = item.Key;
                            DeleteMissScriptTool.DeleteMissScript(prefabPath, scriptFileID);
                            Debug.LogFormat("prefab:{0},script:{1},guid:{2}", prefabPath, scriptFileID, scriptGUID);
                        }
                    }
                }
            }

            EditorUtility.ClearProgressBar();
        }


        void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            if (GUILayout.Button("[检查Prefab脚本Miss情况]", GUILayout.Width(300)))
            {
                PrintMissScriptNoGUID();
                PrintMissScriptWithGUID();
            }

            if (GUILayout.Button("[删除所有Prefab上的Miss脚本]", GUILayout.Width(300)))
            {
                DeleteMissScriptNoGUID();
                DeleteMissScriptWithGUID();
                AssetDatabase.Refresh();
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.EndScrollView();
        }
    }
}
