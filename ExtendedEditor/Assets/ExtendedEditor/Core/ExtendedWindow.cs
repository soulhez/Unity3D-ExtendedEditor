﻿#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using UnityEditor;

namespace TNRD {

	public class ExtendedWindow {

		public ExtendedEditor Editor;

		public ExtendedWindowSettings Settings;

		public bool IsInitialized = false;

		#region GUI.Window 
		public GUIContent WindowContent = new GUIContent();
		public Rect WindowRect = new Rect();
		public GUIStyle WindowStyle = null;
		#endregion

		public Vector2 Position {
			get {
				return WindowRect.position;
			}
		}
		public Vector2 Size {
			get {
				return WindowRect.size;
			}
		}

		public ExtendedInput Input { get { return Editor.Input; } }

		[JsonProperty]
		protected List<ExtendedControl> Controls = new List<ExtendedControl>();
		[JsonIgnore]
		protected List<ExtendedControl> ControlsToProcess = new List<ExtendedControl>();
		[JsonProperty]
		private Dictionary<Type, List<ExtendedControl>> controlsDict = new Dictionary<Type, List<ExtendedControl>>();

		[JsonIgnore]
		private Vector2 previousEditorSize;

		[JsonIgnore]
		private bool initializedGUI = false;

		private ExtendedWindow() { }
		public ExtendedWindow( ExtendedWindowSettings settings ) {
			Settings = settings;
		}

		#region Initialization
		public virtual void OnInitialize() {
			WindowRect = new Rect( 0, 0, Editor.position.size.x, Editor.position.size.y );
			IsInitialized = true;
		}

		protected virtual void OnInitializeGUI() {
			notificationBackgroundStyle = new GUIStyle( "NotificationBackground" );
			notificationTextStyle = new GUIStyle( "NotificationText" );
			notificationTextStyle.padding = new RectOffset( 20, 20, 20, 20 );
			notificationTextStyle.fontSize = 17;
			initializedGUI = true;
		}

		public virtual void OnDeserialized() {
			for ( int i = 0; i < Controls.Count; i++ ) {
				Controls[i].Window = this;
				Controls[i].OnDeserialized();
			}
		}

		public virtual void OnDestroy() {
			IsInitialized = false;
		}
		#endregion

		public virtual void OnFocus() { }
		public virtual void OnLostFocus() { }

		public virtual void Update( bool hasFocus ) {
			ControlsToProcess = new List<ExtendedControl>( Controls );

			if ( Settings.Fullscreen ) {
				var currentEditorSize = Editor.position.size;
				if ( currentEditorSize != previousEditorSize ) {
					WindowRect.size = currentEditorSize;
				}

				previousEditorSize = currentEditorSize;
			}

			foreach ( var item in ControlsToProcess ) {
				item.Update( hasFocus );
			}

			for ( int i = notifications.Count - 1; i >= 0; i-- ) {
				var item = notifications[i];
				if ( item.Duration > 0 && item.Color.a < 1 ) {
					item.Color.a += Editor.DeltaTime * 5;
				} else if ( item.Duration > 0 && item.Color.a >= 1 ) {
					item.Duration -= Editor.DeltaTime;
				} else if ( item.Duration <= 0 && item.Color.a > 0 ) {
					item.Color.a -= Editor.DeltaTime * 5;
				} else if ( item.Duration <= 0 && item.Color.a <= 0 ) {
					notifications.RemoveAt( i );
				}
			}
		}

		#region GUI
		public void InternalGUI( int id ) {
			if ( !initializedGUI ) {
				OnInitializeGUI();
			}

			var e = Editor.CurrentEvent;

			if ( WindowRect.Contains( e.mousePosition ) ) {
				switch ( e.type ) {
					case EventType.ContextClick:
						OnContextClick( e.mousePosition );
						break;
					case EventType.DragPerform:
						OnDragPerform( DragAndDrop.paths, e.mousePosition );
						break;
					case EventType.DragUpdated:
						OnDragUpdate( DragAndDrop.paths, e.mousePosition );
						break;
				}
			}

			switch ( e.type ) {
				case EventType.DragExited:
					OnDragExited();
					break;
				case EventType.ScrollWheel:
					OnScrollWheel( e.delta );
					break;
			}

			BeginGUI();
			OnGUI();
			EndGUI();
		}
		public void BeginGUI() {
			foreach ( var item in ControlsToProcess ) {
				item.OnGUI();
			}

			if ( Settings.DrawToolbar ) {
				ExtendedGUI.BeginToolbar();
				OnToolbarGUI();
				ExtendedGUI.EndToolbar();
			}
		}
		public virtual void OnToolbarGUI() { }
		public virtual void OnGUI() { }
		public void EndGUI() {
			var backgroundColor = GUI.backgroundColor;
			var color = GUI.color;
			for ( int i = notifications.Count - 1; i >= 0; i-- ) {
				var item = notifications[i];

				var xp = Size.x - item.Size.x - 20;
				var yp = Size.y - item.Size.y - 20 - ( i * ( item.Size.y + 5 ) );

				GUI.backgroundColor = GUI.color = item.Color;
				GUI.Box( new Rect( xp, yp, item.Size.x, item.Size.y ), "", notificationBackgroundStyle );
				GUI.Label( new Rect( xp, yp, item.Size.x, item.Size.y ), item.Text, notificationTextStyle );
			}
			GUI.backgroundColor = backgroundColor;
			GUI.color = color;
		}
		#endregion

		#region Controls
		public virtual void AddControl( ExtendedControl control ) {
			if ( Controls.Contains( control ) ) return;

			control.Window = this;

			if ( !control.IsInitialized ) {
				control.OnInitialize();
			}

			var type = control.GetType();
			if ( !controlsDict.ContainsKey( type ) ) {
				controlsDict.Add( type, new List<ExtendedControl>() );
			}

			controlsDict[type].Add( control );
			Controls.Add( control );
		}

		public virtual void RemoveControl( ExtendedControl control ) {
			if ( control.IsInitialized ) {
				control.OnDestroy();
			}

			controlsDict[control.GetType()].Remove( control );
			Controls.Remove( control );
		}

		public List<T> GetControls<T>() where T : ExtendedControl {
			var type = typeof(T);
			if ( controlsDict.ContainsKey( type ) ) {
				var items = new List<T>();
				foreach ( var item in controlsDict[type] ) {
					items.Add( item as T );
				}
				return items;
			} else {
				return new List<T>();
			}
		}

		public List<ExtendedControl> GetControls( Type type ) {
			if ( controlsDict.ContainsKey( type ) ) {
				return controlsDict[type];
			} else {
				return new List<ExtendedControl>();
			}
		}

		public List<T> GetControlsSlow<T>() where T : ExtendedControl {
			var type = typeof(T);
			var list = new List<T>();

			foreach ( var item in Controls ) {
				var baseType = item.GetType().BaseType;
				while ( baseType != null ) {
					if ( baseType == type ) {
						list.Add( item as T );
						break;
					}
					baseType = baseType.BaseType;
				}
			}

			return list;
		}

		public List<ExtendedControl> GetControlsSlow( Type type ) {
			var list = new List<ExtendedControl>();

			foreach ( var item in Controls ) {
				var baseType = item.GetType().BaseType;
				while ( baseType != null ) {
					if ( baseType == type ) {
						list.Add( item );
						break;
					}
					baseType = baseType.BaseType;
				}
			}

			return list;
		}
		#endregion

		#region Events
		public virtual void OnContextClick( Vector2 position ) {
			for ( int i = ControlsToProcess.Count - 1; i >= 0; i-- ) {
				ControlsToProcess[i].OnContextClick( position );
			}
		}
		public virtual void OnDragExited() {
			for ( int i = ControlsToProcess.Count - 1; i >= 0; i-- ) {
				ControlsToProcess[i].OnDragExited();
			}
		}
		public virtual void OnDragPerform( string[] paths, Vector2 position ) {
			for ( int i = ControlsToProcess.Count - 1; i >= 0; i-- ) {
				ControlsToProcess[i].OnDragPerform( paths, position );
			}
		}
		public virtual void OnDragUpdate( string[] paths, Vector2 position ) {
			for ( int i = ControlsToProcess.Count - 1; i >= 0; i-- ) {
				ControlsToProcess[i].OnDragUpdate( paths, position );
			}
		}
		public virtual void OnScrollWheel( Vector2 delta ) {
			for ( int i = ControlsToProcess.Count - 1; i >= 0; i-- ) {
				ControlsToProcess[i].OnScrollWheel( delta );
			}
		}
		#endregion

		#region Notifications
		[JsonIgnore]
		private List<ExtendedNotification> notifications = new List<ExtendedNotification>();
		[JsonIgnore]
		private GUIStyle notificationBackgroundStyle;
		[JsonIgnore]
		private GUIStyle notificationTextStyle;

		public void ShowNotification( string text ) {
			ShowNotification( text, Color.white, 1.25f );
		}
		public void ShowNotification( string text, Color color, float duration ) {
			if ( string.IsNullOrEmpty( text ) ) return;
			color.a = 0;
			notifications.Add( new ExtendedNotification( text, color, duration, notificationTextStyle ) );
		}
		#endregion
	}
}
#endif