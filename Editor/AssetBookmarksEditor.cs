using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

/*
    Author: Bjørn Slettemark
    Email: bjorn.slettemark@outlook.com

    Description:
    The Asset Bookmarks Editor is a Unity Editor extension designed to enhance the workflow by allowing users to bookmark assets for quick access.
    This tool enables users to easily add, sort, and access frequently used assets, making project management more efficient within the Unity Editor.

    Usage:
    - To add assets to the bookmarks list, select the desired assets in the Project window and choose "Assets > Add to Bookmarks" from the menu or drag and drop them into the Asset Bookmarks window.
    - Open the Asset Bookmarks window by navigating to "Window > Asset Management > Asset Bookmarks" from the Unity Editor menu.
    - Assets can be sorted within the window by Name, Type, Time Added, or Asset extension. Click on the respective column header to sort the bookmarks accordingly.
    - Individual bookmarks can be removed by clicking the "-" button next to each asset in the list.

    The bookmark list is saved between Unity sessions, ensuring that your bookmarks are retained for ongoing and future work.
*/

public class AssetBookmarksEditor : EditorWindow
{
    private static List<UnityEngine.Object> bookmarkedObjects = new List<UnityEngine.Object>();
    private UnityEngine.Object selectedObject;
    private enum SortType { Type, Name, TimeAdded, Asset }
    private SortType currentSortType = SortType.Name;
    private bool sortAscending = true;
    private GUIStyle headerStyle;

    // Constants for PlayerPrefs keys
    private const string BookmarkKeyPrefix = "Bookmark_";
    private const string SortTypeKey = "SortType";
    private const string SortAscendingKey = "SortAscending";

    [MenuItem("Assets/Add to Bookmarks", false, 1200)]
    public static void AddToBookmarks()
    {
        UnityEngine.Object[] newObjects = Selection.objects;
        foreach (UnityEngine.Object newObject in newObjects)
        {
            if (newObject != null && !bookmarkedObjects.Contains(newObject))
            {
                bookmarkedObjects.Add(newObject);
            }
        }
        SaveBookmarks();
    }

    [MenuItem("Window/Asset Management/Asset Bookmarks", false, 1800)]
    public static void ShowWindow()
    {
        AssetBookmarksEditor window = (AssetBookmarksEditor)EditorWindow.GetWindow(typeof(AssetBookmarksEditor));
        GUIContent titleContent = new GUIContent("Asset Bookmarks", EditorGUIUtility.IconContent("Favorite").image);
        window.titleContent = titleContent;
        LoadBookmarks();
    }

    void OnEnable()
    {
        LoadSortSettings();
        LoadBookmarks();
    }

    void OnDisable()
    {
        SaveSortSettings();
        SaveBookmarks();
    }

    void OnGUI()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.label);
            headerStyle.fontSize = 12;
        }

        Event evt = Event.current;

        // Define drop area covering the entire window
        Rect dropArea = new Rect(0, 0, position.width, position.height);

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition))
                    break;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
                    {
                        if (!bookmarkedObjects.Contains(draggedObject))
                        {
                            bookmarkedObjects.Add(draggedObject);
                        }
                    }

                    SaveBookmarks();
                }
                break;
        }

        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        DrawSortableHeader("Name", SortType.Name);
        DrawSortableHeader("Asset", SortType.Asset);
        DrawSortableHeader("Type", SortType.Type);
        DrawSortableHeader("Date", SortType.TimeAdded);
        GUILayout.EndHorizontal();

        GUI.Box(dropArea, "");

        UnityEngine.Object objectToRemove = null;

        if (bookmarkedObjects.Count > 0)
        {
            ScrollPosition = GUILayout.BeginScrollView(ScrollPosition, false, true);
            foreach (UnityEngine.Object obj in bookmarkedObjects)
            {
                if (obj == null) continue; // Skip null entries

                GUILayout.BeginHorizontal();

                Texture icon = AssetPreview.GetMiniThumbnail(obj);
                GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));

                string objectName = GetObjectNameWithParentFolder(obj);
                if (GUILayout.Button(objectName))
                {
                    selectedObject = obj;
                    EditorGUIUtility.PingObject(obj);
                    Selection.activeObject = obj;
                }

                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    objectToRemove = obj;
                    // Note: We don't break here to ensure GUI layout consistency
                }

                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        if (objectToRemove != null)
        {
            bookmarkedObjects.Remove(objectToRemove);
            SaveBookmarks(); // Remember to save changes
        }
    }
    private static string GetObjectNameWithParentFolder(UnityEngine.Object obj)
    {
        string objectName = obj.name;
        string assetPath = AssetDatabase.GetAssetPath(obj);

        // Check if the asset is a folder
        if (!string.IsNullOrEmpty(assetPath) && System.IO.Directory.Exists(assetPath))
        {
            // The asset is a folder, extract its parent folder name
            string parentFolder = System.IO.Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(parentFolder))
            {
                // Only add the parent folder if it's not the root Assets folder
                string parentFolderName = System.IO.Path.GetFileName(parentFolder);
                if (!string.IsNullOrEmpty(parentFolderName))
                {
                    objectName = parentFolderName + " / " + objectName;
                }
            }
        }
        // For non-folder assets, you might not want to change the display name
        // Or handle differently if required
        return objectName;
    }

    private void DrawSortableHeader(string headerName, SortType sortType)
    {
        GUILayout.BeginHorizontal();
        bool isCurrentSortType = currentSortType == sortType;

        // Use a fixed-width style for the arrow to ensure layout consistency
        GUILayout.Label(isCurrentSortType ? (sortAscending ? "▲" : "▼") : "  ", GUILayout.Width(20));

        if (GUILayout.Button(headerName, headerStyle))
        {
            if (isCurrentSortType)
            {
                sortAscending = !sortAscending;
            }
            else
            {
                currentSortType = sortType;
                sortAscending = true;
            }

            SortBookmarkedObjects();
        }

        GUILayout.EndHorizontal();
    }



    private void SortBookmarkedObjects()
    {
        if (currentSortType == SortType.TimeAdded && !sortAscending)
        {
            // Special handling for Time Added in descending order.
            bookmarkedObjects.Reverse();
        }
        else
        {
            // Reset to original order before applying sort, if necessary.
            LoadBookmarks(); // Reload original order or maintain a separate list representing the original order.

            switch (currentSortType)
            {
                case SortType.Type:
                    bookmarkedObjects.Sort((a, b) => string.Compare(a.GetType().Name, b.GetType().Name, StringComparison.Ordinal) * (sortAscending ? 1 : -1));
                    break;
                case SortType.Name:
                    bookmarkedObjects.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal) * (sortAscending ? 1 : -1));
                    break;
                // Skip Time Added because we handle it above.
                case SortType.Asset:
                    bookmarkedObjects.Sort((a, b) =>
                    {
                        string extensionA = System.IO.Path.GetExtension(AssetDatabase.GetAssetPath(a)).ToLower();
                        string extensionB = System.IO.Path.GetExtension(AssetDatabase.GetAssetPath(b)).ToLower();
                        return string.Compare(extensionA, extensionB, StringComparison.Ordinal) * (sortAscending ? 1 : -1);
                    });
                    break;
            }
        }
    }


    private void LoadSortSettings()
    {
        currentSortType = (SortType)EditorPrefs.GetInt(SortTypeKey, (int)SortType.Name);
        sortAscending = EditorPrefs.GetBool(SortAscendingKey, true);
    }

    private void SaveSortSettings()
    {
        EditorPrefs.SetInt(SortTypeKey, (int)currentSortType);
        EditorPrefs.SetBool(SortAscendingKey, sortAscending);
    }

    private static void LoadBookmarks()
    {
        string projectKeyPrefix = Application.dataPath.Replace("/", "_").Replace("\\", "_") + "_";
        bookmarkedObjects.Clear();
        int count = EditorPrefs.GetInt(projectKeyPrefix + "BookmarkCount", 0);
        for (int i = 0; i < count; i++)
        {
            string key = projectKeyPrefix + BookmarkKeyPrefix + i;
            string assetPath = EditorPrefs.GetString(key, "");
            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (obj != null)
            {
                bookmarkedObjects.Add(obj);
            }
        }
    }

    private static void SaveBookmarks()
    {
        string projectKeyPrefix = Application.dataPath.Replace("/", "_").Replace("\\", "_") + "_";
        EditorPrefs.SetInt(projectKeyPrefix + "BookmarkCount", bookmarkedObjects.Count);
        for (int i = 0; i < bookmarkedObjects.Count; i++)
        {
            string key = projectKeyPrefix + BookmarkKeyPrefix + i;
            string assetPath = AssetDatabase.GetAssetPath(bookmarkedObjects[i]);
            EditorPrefs.SetString(key, assetPath);
        }
    }

    private Vector2 ScrollPosition;
}
