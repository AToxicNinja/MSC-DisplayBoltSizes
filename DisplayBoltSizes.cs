using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using MSCLoader;
using UnityEngine;

namespace DisplayBoltSize
{
    public enum SizingMode
    {
        Number,
        Direction,
        DirectionDescriptive
    }

    public class DisplayBoltSize : Mod
    {
        public override string ID => "DisplayBoltSize"; //Your mod ID (unique)
        public override string Name => "Display Bolt Size"; //You mod name
        public override string Author => "AToxicNinja"; //Your Username
        public override string Version => "1.0"; //Version
        public override string Description => "Display the size of the bolt/required wrench size, or if the wrench is too large, or too small. Re-written/taken from Lex's Show Bolt Size remake of wolf_vx's mod. Thats a mouthful."; //Short description of your mod
        public override bool UseAssetsFolder => false;

        private class FsmHookAction : FsmStateAction
        {
            public Action hook;
            public override void OnEnter()
            {
                hook?.Invoke();
                Finish();
            }
        }

        // Settings variables
        string[] dropDownValues = new string[] { "Show Bolt Size", "Show Directionality", "Show Descriptive Directionality" };

        // PlayMaker Variables
        private FsmFloat boltSize;
        private FsmString interaction;
        private FsmFloat toolWrenchSize;

        // Mod instance variables
        private bool hasInjected = false;
        private string interactionText;
        private SizingMode sizingMode = SizingMode.Number;

        public override void ModSetup()
        {
            SetupFunction( Setup.OnLoad, Mod_OnLoad );
            SetupFunction( Setup.Update, Mod_Update );
        }

        private void Mod_OnLoad()
        {
            // Called once, when mod is loading after game is fully loaded
            toolWrenchSize = PlayMakerGlobals.Instance.Variables.GetFsmFloat( "ToolWrenchSize" );
            interaction = PlayMakerGlobals.Instance.Variables.GetFsmString( "GUIinteraction" );
            GameObject selectItem = GameObject.Find( "PLAYER/Pivot/AnimPivot/Camera/FPSCamera/SelectItem" );
            FsmHook.FsmInject( selectItem, "Tools", getBoltSizeFsmFloat );
        }

        public override void ModSettings()
        {
            // All settings should be created here. 
            // DO NOT put anything else here that settings or keybinds
            Settings.AddHeader( this, "Bolt Sizing Mode" );
            Settings.AddDropDownList( this, "boltSizingList", "How should bolt sizes be conveyed?", dropDownValues, 0, settingsUpdated );
        }

        public override void ModSettingsLoaded()
        {
            // Call some functions here to update stuff when settings values are loaded
            settingsUpdated();
        }

        private void Mod_Update()
        {
            if( !hasInjected && toolWrenchSize != null )
            {
                float _wrenchSize = toolWrenchSize.Value;
                if( _wrenchSize != 0 )
                {
                    // We must be holding wrench before this is injected so that the states/actions are active.
                    GameObject wrenchRaycast = GameObject.Find( "PLAYER/Pivot/AnimPivot/Camera/FPSCamera/2Spanner/Raycast" );
                    PlayMakerFSM[] components = wrenchRaycast.GetComponents<PlayMakerFSM>();
                    Fsm bolt = null;
                    foreach( PlayMakerFSM component in components )
                    {
                        if( component.FsmName == "Check" )
                        {
                            bolt = component.Fsm;
                            break;
                        }
                    }
                    FsmInjectMock( bolt, "State 1", checkBoltSizingCallback );
                    FsmInjectMock( bolt, "State 2", checkBoltSizingCallback );
                    hasInjected = true;
                }
                return;
            }
        }

        private void settingsUpdated()
        {
            List<Settings> testSettings = Settings.Get( this );
            foreach( Settings setting in testSettings )
            {
                if( setting.ID == "boltSizingList" )
                {
                    switch( setting.Value )
                    {
                        case 2:
                            sizingMode = SizingMode.DirectionDescriptive;
                            break;
                        case 1:
                            sizingMode = SizingMode.Direction;
                            break;
                        case 0:
                        default:
                            sizingMode = SizingMode.Number;
                            break;
                    }
                }
            }
        }

        private void getBoltSizeFsmFloat()
        {
            if( boltSize == null )
            {
                GameObject wrenchRaycast = GameObject.Find( "PLAYER/Pivot/AnimPivot/Camera/FPSCamera/2Spanner/Raycast" );
                PlayMakerFSM[] components = wrenchRaycast.GetComponents<PlayMakerFSM>();
                Fsm bolt = null;
                foreach( PlayMakerFSM component in components )
                {
                    if( component.FsmName == "Check" )
                    {
                        bolt = component.Fsm;
                        break;
                    }
                }
                boltSize = bolt.GetFsmFloat( "BoltSize" );

                // Inject bolt check callback to States related to bolts would normally be done here,
                // but I keep getting errors trying to do that, as the actions aren't yet activated on the states
                // so nothing can be added/modified yet. I have changed to an approach where it is checked for in update instead.
            }
        }

        private void checkBoltSizingCallback()
        {
            // Get wrench sizing
            int currentWrenchSize = Mathf.RoundToInt( toolWrenchSize.Value * 10f );

            // Get bolt sizing
            int currentBoltSize = Mathf.RoundToInt( boltSize.Value * 10f );

            if( currentWrenchSize == currentBoltSize )
            {
                return; // Nothing to do, correct size
            }

            if( sizingMode == SizingMode.Number )
            {
                interactionText = $"Size {currentBoltSize}";
            }
            else if( sizingMode == SizingMode.Direction )
            {
                string direction = currentWrenchSize < currentBoltSize ? "Small" : "Big";
                interactionText = $"Wrench Too {direction}";
            }
            else if( sizingMode == SizingMode.DirectionDescriptive )
            {
                string direction = currentWrenchSize < currentBoltSize ? "Small" : "Big";
                int dist = Math.Abs( currentWrenchSize - currentBoltSize );
                string quantifier;
                if( dist > 3 )
                {
                    quantifier = "Way";
                }
                else
                {
                    quantifier = "Slightly";
                }

                interactionText = $"Wrench {quantifier} Too {direction}";
            }
            interaction.Value = interactionText;
        }

        private void FsmInjectMock( Fsm fsm, string stateName, Action callback )
        {
            bool foundState = false;
            FsmState[] states = fsm.States;
            if( states != null )
            {
                foreach( FsmState state in states )
                {
                    if( state != null && state.Name == stateName )
                    {
                        foundState = true;
                        // inject our hook action to the state machine
                        List<FsmStateAction> actions = new List<FsmStateAction>( state.Actions );
                        FsmHookAction hookAction = new FsmHookAction
                        {
                            hook = callback
                        };
                        actions.Insert( 0, hookAction );
                        state.Actions = actions.ToArray();
                    }
                }
            }
            if( !foundState )
            {
                ModConsole.Error( string.Format( "FsmInjectMock: Cannot find state <b>{0}</b>", stateName ) );
            }
        }
    }
}
