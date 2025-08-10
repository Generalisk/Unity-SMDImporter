//=================================================================================
// Copyright (c) 2025 Generalisk
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//=================================================================================

using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Generalisk.Editor.Importers.SMD
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(SMDImporter))]
    class SMDImporterEditor : AssetImporterEditor
    {
        private int tab = 0;

        public override void OnInspectorGUI()
        {
            tab = GUILayout.Toolbar(tab, new string[] {
                "Model", "Materials",
            });
            EditorGUILayout.Space(15);

            switch (tab)
            {
                case 0:
                    SerializedProperty scale = serializedObject.FindProperty("scale");
                    SerializedProperty forceRig = serializedObject.FindProperty("forceRig");

                    EditorGUILayout.PropertyField(scale, new GUIContent("Scale"));
                    EditorGUILayout.PropertyField(forceRig, new GUIContent("Forcibly Add Rig"));
                    break;
                case 1:
                    SerializedProperty matNames = serializedObject.FindProperty("materialNames");
                    SerializedProperty materials = serializedObject.FindProperty("materials");

                    for (int i = 0; i < materials.arraySize; i++)
                    {
                        SerializedProperty matName = matNames.GetArrayElementAtIndex(i);
                        SerializedProperty material = materials.GetArrayElementAtIndex(i);
                        EditorGUILayout.PropertyField(material, new GUIContent(matName.stringValue));
                    }
                    break;
                default:
                    EditorGUILayout.HelpBox("No tab selected!", MessageType.Error);
                    break;
            }

            ApplyRevertGUI();
        }
    }
}
