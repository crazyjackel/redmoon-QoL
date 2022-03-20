#if UNITY_EDITOR 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace RedMoon.StateMachines.Editor
{
    [CustomPropertyDrawer(typeof(UnityEventBase), true)]
    public class UnityEventDrawerCallback : UnityEventDrawer
    {
        private const string kNoFunctionString = "No Function";

        //Persistent Listener Paths
        internal const string kInstancePath = "m_Target";
        internal const string kInstanceTypePath = "m_TargetAssemblyTypeName";
        internal const string kCallStatePath = "m_CallState";
        internal const string kArgumentsPath = "m_Arguments";
        internal const string kModePath = "m_Mode";
        internal const string kMethodNamePath = "m_MethodName";

        //ArgumentCache paths
        internal const string kFloatArgument = "m_FloatArgument";
        internal const string kIntArgument = "m_IntArgument";
        internal const string kObjectArgument = "m_ObjectArgument";
        internal const string kStringArgument = "m_StringArgument";
        internal const string kBoolArgument = "m_BoolArgument";
        internal const string kObjectArgumentAssemblyTypeName = "m_ObjectArgumentAssemblyTypeName";

        protected override void DrawEvent(Rect rect, int index, bool isActive, bool isFocused)
        {
            UnityEventBase m_DummyEvent = (UnityEventBase)typeof(UnityEventDrawer).GetField("m_DummyEvent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this);
            SerializedProperty m_ListenersArray = (SerializedProperty)typeof(UnityEventDrawer).GetField("m_ListenersArray", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this);
            var pListener = m_ListenersArray.GetArrayElementAtIndex(index);


            var getrowrects = typeof(UnityEventDrawer).GetMethod("GetRowRects", BindingFlags.NonPublic | BindingFlags.Instance);
            rect.y++;
            Rect[] subRects = (Rect[])getrowrects.Invoke(this, new object[] { rect });
            Rect enabledRect = subRects[0];
            Rect goRect = subRects[1];
            Rect functionRect = subRects[2];
            Rect argRect = subRects[3];

            // find the current event target...
            var callState = pListener.FindPropertyRelative(kCallStatePath);
            var mode = pListener.FindPropertyRelative(kModePath);
            var arguments = pListener.FindPropertyRelative(kArgumentsPath);
            var listenerTarget = pListener.FindPropertyRelative(kInstancePath);
            var methodName = pListener.FindPropertyRelative(kMethodNamePath);

            Color c = GUI.backgroundColor;
            GUI.backgroundColor = Color.white;

            EditorGUI.PropertyField(enabledRect, callState, GUIContent.none);

            EditorGUI.BeginChangeCheck();
            {
                GUI.Box(goRect, GUIContent.none);
                EditorGUI.PropertyField(goRect, listenerTarget, GUIContent.none);
                if (EditorGUI.EndChangeCheck())
                    methodName.stringValue = null;
            }

            SerializedProperty argument;
            var getmode = typeof(UnityEventDrawer).GetMethod("GetMode", BindingFlags.NonPublic | BindingFlags.Static);
            PersistentListenerMode modeEnum = (PersistentListenerMode)getmode.Invoke(null, new object[] { mode });
            //only allow argument if we have a valid target / method
            if (listenerTarget.objectReferenceValue == null || string.IsNullOrEmpty(methodName.stringValue))
                modeEnum = PersistentListenerMode.Void;

            switch (modeEnum)
            {
                case PersistentListenerMode.Float:
                    argument = arguments.FindPropertyRelative(kFloatArgument);
                    break;
                case PersistentListenerMode.Int:
                    argument = arguments.FindPropertyRelative(kIntArgument);
                    break;
                case PersistentListenerMode.Object:
                    argument = arguments.FindPropertyRelative(kObjectArgument);
                    break;
                case PersistentListenerMode.String:
                    argument = arguments.FindPropertyRelative(kStringArgument);
                    break;
                case PersistentListenerMode.Bool:
                    argument = arguments.FindPropertyRelative(kBoolArgument);
                    break;
                default:
                    argument = arguments.FindPropertyRelative(kIntArgument);
                    break;
            }

            var desiredArgTypeName = arguments.FindPropertyRelative(kObjectArgumentAssemblyTypeName).stringValue;
            var desiredType = typeof(Object);
            if (!string.IsNullOrEmpty(desiredArgTypeName))
                desiredType = Type.GetType(desiredArgTypeName, false) ?? typeof(Object);

            if (modeEnum == PersistentListenerMode.Object)
            {
                EditorGUI.BeginChangeCheck();
                var result = EditorGUI.ObjectField(argRect, GUIContent.none, argument.objectReferenceValue, desiredType, true);
                if (EditorGUI.EndChangeCheck())
                    argument.objectReferenceValue = result;
            }
            else if (modeEnum != PersistentListenerMode.Void && modeEnum != PersistentListenerMode.EventDefined)
                EditorGUI.PropertyField(argRect, argument, GUIContent.none);

            using (new EditorGUI.DisabledScope(listenerTarget.objectReferenceValue == null))
            {
                EditorGUI.BeginProperty(functionRect, GUIContent.none, methodName);
                {
                    GUIContent buttonContent;
                    if (EditorGUI.showMixedValue)
                    {
                        GUIContent content = (GUIContent)typeof(EditorGUI).GetProperty("mixedValueContent").GetValue(null);
                        buttonContent = content;
                    }
                    else
                    {
                        var buttonLabel = new StringBuilder();
                        if (listenerTarget.objectReferenceValue == null || string.IsNullOrEmpty(methodName.stringValue))
                        {
                            buttonLabel.Append(kNoFunctionString);
                        }
                        else if (!IsPersistantListenerValid(m_DummyEvent, methodName.stringValue, listenerTarget.objectReferenceValue, modeEnum, desiredType))
                        {
                            var instanceString = "UnknownComponent";
                            var instance = listenerTarget.objectReferenceValue;
                            if (instance != null)
                                instanceString = instance.GetType().Name;

                            buttonLabel.Append(string.Format("<Missing {0}.{1}>", instanceString, methodName.stringValue));
                        }
                        else
                        {
                            buttonLabel.Append(listenerTarget.objectReferenceValue.GetType().Name);

                            if (!string.IsNullOrEmpty(methodName.stringValue))
                            {
                                buttonLabel.Append(".");
                                if (methodName.stringValue.StartsWith("set_"))
                                    buttonLabel.Append(methodName.stringValue.Substring(4));
                                else
                                    buttonLabel.Append(methodName.stringValue);
                            }
                        }
                        buttonContent = (GUIContent)typeof(GUIContent).GetMethod("Temp",
                            BindingFlags.NonPublic | BindingFlags.Static,
                            null,
                            CallingConventions.Any,
                            new Type[] { typeof(string) },
                            null).Invoke(null, new object[] { buttonLabel.ToString() });
                    }

                    if (EditorGUI.DropdownButton(functionRect, buttonContent, FocusType.Passive, EditorStyles.popup))
                        BuildPopupList(listenerTarget.objectReferenceValue, m_DummyEvent, pListener).DropDown(functionRect);
                }
                EditorGUI.EndProperty();
            }
            GUI.backgroundColor = c;
        }
        internal static GenericMenu BuildPopupList(Object target, UnityEventBase dummyEvent, SerializedProperty listener)
        {
            //special case for components... we want all the game objects targets there!
            var targetToUse = target;
            if (targetToUse is Component)
                targetToUse = (target as Component).gameObject;

            // find the current event target...
            var methodName = listener.FindPropertyRelative(kMethodNamePath);

            var menu = new GenericMenu();

            var method = typeof(UnityEventDrawer)
                .GetMethod("ClearEventFunction",
                BindingFlags.Static | BindingFlags.NonPublic);

            var obj = Activator.CreateInstance(typeof(UnityEventDrawer)
                .GetNestedType("UnityEventFunction",
                BindingFlags.Instance | BindingFlags.NonPublic),
                listener,
                null,
                null,
                PersistentListenerMode.EventDefined);

            menu.AddItem(new GUIContent(kNoFunctionString),
                string.IsNullOrEmpty(methodName.stringValue),
                (GenericMenu.MenuFunction2)Delegate.CreateDelegate(typeof(UnityEditor.GenericMenu.MenuFunction2), method),
                obj);

            if (targetToUse == null)
                return menu;

            menu.AddSeparator("");

            // figure out the signature of this delegate...
            // The property at this stage points to the 'container' and has the field name
            Type delegateType = dummyEvent.GetType();

            // check out the signature of invoke as this is the callback!
            MethodInfo delegateMethod = delegateType.GetMethod("Invoke");
            var delegateArgumentsTypes = delegateMethod.GetParameters().Select(x => x.ParameterType).ToArray();

            var duplicateNames = new Dictionary<string, int>();
            var duplicateFullNames = new Dictionary<string, int>();

            var genpop = typeof(UnityEventDrawer).GetMethod("GeneratePopUpForType", BindingFlags.NonPublic | BindingFlags.Static);

            genpop.Invoke(null, new object[] { menu, targetToUse, targetToUse.GetType().Name, listener, delegateArgumentsTypes });
            duplicateNames[targetToUse.GetType().Name] = 0;
            if (targetToUse is GameObject)
            {
                Component[] comps = (targetToUse as GameObject).GetComponents<Component>();

                // Collect all the names and record how many times the same name is used.
                foreach (Component comp in comps)
                {
                    var duplicateIndex = 0;
                    if (duplicateNames.TryGetValue(comp.GetType().Name, out duplicateIndex))
                        duplicateIndex++;
                    duplicateNames[comp.GetType().Name] = duplicateIndex;
                }

                foreach (Component comp in comps)
                {
                    if (comp == null)
                        continue;

                    var compType = comp.GetType();
                    string targetName = compType.Name;
                    int duplicateIndex = 0;

                    // Is this name used multiple times? If so then use the full name plus an index if there are also duplicates of this. (case 1309997)
                    if (duplicateNames[compType.Name] > 0)
                    {
                        if (duplicateFullNames.TryGetValue(compType.FullName, out duplicateIndex))
                            targetName = $"{compType.FullName} ({duplicateIndex})";
                        else
                            targetName = compType.FullName;
                    }
                    genpop.Invoke(null, new object[] { menu, comp, targetName, listener, delegateArgumentsTypes });
                    duplicateFullNames[compType.FullName] = duplicateIndex + 1;

                    var fields = comp.GetType().GetFields();
                    foreach (var property in fields)
                    {
                        if (typeof(Object).IsAssignableFrom(property.FieldType))
                        {
                            Object prop = null;
                            try
                            {
                                prop = (Object)property.GetValue(comp);
                            }
                            catch (Exception ex)
                            {
                                Debug.Log(ex);
                                continue;
                            }
                            if (prop == null) continue;
                            var objProp = (Object)prop;

                            string targetName2 = $"{targetName} -  {prop.GetType()}";


                            genpop.Invoke(null, new object[] { menu, objProp, targetName2, listener, delegateArgumentsTypes });
                        }
                    }
                }
            }
            return menu;
        }
    }
}

#endif