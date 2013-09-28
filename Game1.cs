using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System.Timers;
using Plantronics.Innovation.PLTLabsAPI;

/*******
 * 
 * Head Tracking Target Demo
 * 
 * A demo sample project created for Plantronics PLTLabs innovation blog.
 * 
 * This application shows the following:
 * 
 *   - A game program that integrates support for Plantronics innovation head tracking
 *     
 *   - Uses headtracking angles to guide a target sight (reticle) on the game screen
 *   
 * PRE-REQUISITES for building this demo app:
 *  - Plantronics Spokes 3.0 SDK - install PlantronicsSpokesSDKInstaller.exe
 *  - Microsoft Visual Studio 2010 - obtain from http://www.microsoft.com/visualstudio/eng/products/visual-studio-2010-express
 *  - Microsoft XNA Game Studio 4.0 - obtain from http://www.microsoft.com/en-gb/download/details.aspx?id=23714
 *  (Drawing a Sprite Tutorial: http://msdn.microsoft.com/en-us/library/bb194908.aspx)
 *
 * PRE-REQUISITES for testing this demo app: 
 *  - Current pre-release head-tracking headset with appropriate firmware pre-loaded
 * 
 * ADDITIONAL INTERIM PRE-REQUISITE
 *  - At the time of writing there is also an additional pre-requisite to access headtracking
 *  data on a PC app, this is as follows:
 *    - Head-tracking headset also requires pairing with iPhone running iOS6 as  well as the PC 
 *    via BT300 Dongle.
 *    - The iPhone must also be running the "PLT Sensor" application (If this is still a pre-requisite
 *    as this blog goes to press then please request PLTLabs to join the "TestFlight" program for 
 *    "PLT Sensor" app). This app is needed to "reflect" the headtracking data back to your app on the PC.
 *   
 * INSTRUCTIONS FOR USE
 * 
 *   - If you put headset on and look at center of the screen, after 2 second delay the
 *     head tracking and target sight will "auto-calibrate" to center of screen.
 *     
 *   - From that point on the target sight will track your head movements
 *   
 *   - If you take your headset off the target sight will stop tracking your movements
 *   and place itself back in center of screen
 *   
 *   - Note: F will toggle fullscreen. Escape to quit.
 * 
 * Lewis Collins, http://developer.plantronics.com/people/lcollins/
 * 
 * VERSION HISTORY:
 * ********************************************************************************
 * Version 1.0.0.1:
 * Date: 27th September 2013
 * Changed by: Lewis Collins
 * Changes:
 *   - Updated to use the new PC API DLL for "Concept 1" device
 *
 * Version 1.0.0.0:
 * Date: 17th July 2013
 * Changed by: Lewis Collins
 *   Initial version.
 * ********************************************************************************
 *
 **/

namespace HeadTrackerTargetGame
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game, PLTLabsCallbackHandler
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        private Texture2D SpriteTexture;

        private int m_screenmax_x; // store a copy of max screen coordinate
        private int m_screenmax_y; // store a copy of max screen coordinate
        private int m_halfscreenmax_x; // store a copy of center screen coordinate
        private int m_halfscreenmax_y; // store a copy of center screen coordinate
        private Vector2 m_origin; // hotspot from which to handle the crosshair sprite from (will be placed at center of sprite)

        // Plantronics innovation head tracking...
        Timer m_autoputoncalibratetimer;  // timer to initiate headtracking calibration after short time delay
        private int headtrack_xoffset = 0; // head track heading offset for target
        private int headtrack_yoffset = 0; // head track pitch offset for target
        private bool m_worn = false; // flag to know if headset is worn or not
        private PLTConnection m_pltConnection;
        private PLTLabsAPI m_pltlabsapi;
        private bool m_calibrated;
        private SpriteFont font;
        KeyboardState oldKeyboardState,
                          currentKeyboardState;// Keyboard state variables

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            // timer to auto calibrate
            m_autoputoncalibratetimer = new Timer();
            m_autoputoncalibratetimer.Interval = 2000;
            m_autoputoncalibratetimer.AutoReset = false;
            m_autoputoncalibratetimer.Elapsed += autoputoncalibratetimer_Elapsed;

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your game content here
            SpriteTexture = Content.Load<Texture2D>("targetcrosshairs");
            m_screenmax_x = graphics.GraphicsDevice.Viewport.Width;
            m_screenmax_y = graphics.GraphicsDevice.Viewport.Height;
            m_halfscreenmax_x = m_screenmax_x / 2;
            m_halfscreenmax_y = m_screenmax_y / 2;
            m_origin.X = SpriteTexture.Width / 2;
            m_origin.Y = SpriteTexture.Height / 2;

            font = Content.Load<SpriteFont>("SpriteFont1");

            // Initialise Plantronics head tracking...
            m_pltlabsapi = new PLTLabsAPI(this);
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
            if (m_pltlabsapi!=null)
            {
                m_pltlabsapi.Shutdown();  
            }
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            // TODO: Add your update logic here
            oldKeyboardState = currentKeyboardState;
            currentKeyboardState = Keyboard.GetState();

            // Thanks: http://mort8088.com/2011/03/06/xna-4-0-tutorial-3-input-from-keyboard/
            // Allows the game to exit
            if ((currentKeyboardState.IsKeyUp(Keys.Escape)) && (oldKeyboardState.IsKeyDown(Keys.Escape)))
            {
                this.Exit();
            }

            // allow toggle fullscreen with F key:
            if ((currentKeyboardState.IsKeyUp(Keys.F)) && (oldKeyboardState.IsKeyDown(Keys.F)))
            {
                graphics.ToggleFullScreen();
                m_screenmax_x = graphics.GraphicsDevice.Viewport.Width;
                m_screenmax_y = graphics.GraphicsDevice.Viewport.Height;
                m_halfscreenmax_x = m_screenmax_x / 2;
                m_halfscreenmax_y = m_screenmax_y / 2;
                m_origin.X = SpriteTexture.Width / 2;
                m_origin.Y = SpriteTexture.Height / 2;

            }

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // TODO: Add your drawing code here
            // Draw the head tracker target on the screen offset from the center of the screen
            // by an amount on x/y axis which is based on the user's head movements
            // (for calculation see HeadsetTrackingUpdate function below)
            spriteBatch.Begin();
            Vector2 pos = new Vector2(m_halfscreenmax_x + headtrack_xoffset, m_halfscreenmax_y + headtrack_yoffset);
            spriteBatch.Draw(SpriteTexture, pos, null, Color.White, 0f, m_origin, 1.0f, SpriteEffects.None, 0f );

            if (!m_calibrated)
            {
                spriteBatch.DrawString(font, "Awaiting calibration (place headset on table)", new Vector2(20, 45), Color.White);
            }
            if (m_autoputoncalibratetimer.Enabled)
            {
                spriteBatch.DrawString(font, "Headset put on, about to calibrate (2 seconds)...", new Vector2(20, 25), Color.White);
            }

            spriteBatch.End();

            base.Draw(gameTime);
        }

        void autoputoncalibratetimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // initiate auto calibration
            if (m_pltConnection != null)
            {
                m_pltlabsapi.calibrateService(PLTService.MOTION_TRACKING_SVC);
            }
        }

        // receives headtracking angles in degrees back from PLT Labs API
        public void HeadsetTrackingUpdate(PLTMotionTrackingData headsetData)
        {
            // need to reverse heading and pitch sign?
            headsetData.m_orientation[0] = -headsetData.m_orientation[0];
            //headsetData.m_orientation[1] = -headsetData.m_orientation[1];

            // define some constants for maths calculation to convert head angles into pixel offsets for screen
            const double c_distanceToScreen = 850; // millimeters
            const double c_pixelPitch = 0.25; // millimeters

            double headtrack_offset_millimeters; // temporary variable to hold headset offset

            //if (m_worn)
            //{
                // assume distance from screen is 1 meter and that pixel size is 0.25mm
                headtrack_offset_millimeters = c_distanceToScreen * Math.Tan(headsetData.m_orientation[0] * Math.PI / 180.0); // x = d * tan(theta)
                headtrack_xoffset = (int) (headtrack_offset_millimeters / c_pixelPitch);
                headtrack_offset_millimeters = c_distanceToScreen * Math.Tan(headsetData.m_orientation[1] * Math.PI / 180.0); // y = d * tan(theta)
                headtrack_yoffset = (int)(headtrack_offset_millimeters / c_pixelPitch);
            //}
            //else
            //{
            //    headtrack_xoffset = 0;
            //    headtrack_yoffset = 0;
            //}
        }

        public void ConnectionClosed(PLTDevice pltDevice)
        {
            m_pltConnection = null;
        }

        public void ConnectionFailed(PLTDevice pltDevice)
        {
            m_pltConnection = null;
        }

        public void ConnectionOpen(PLTConnection pltConnection)
        {
            // lets register for some services
            m_pltConnection = pltConnection;

            if (pltConnection != null)
            {
                m_pltlabsapi.subscribe(PLTService.MOTION_TRACKING_SVC, PLTMode.On_Change);
                m_pltlabsapi.configureService(PLTService.MOTION_TRACKING_SVC, PLTConfiguration.MotionSvc_Offset_Calibrated);
                m_pltlabsapi.configureService(PLTService.MOTION_TRACKING_SVC, PLTConfiguration.MotionSvc_Format_Orientation);
                m_pltlabsapi.subscribe(PLTService.SENSOR_CAL_STATE_SVC, PLTMode.On_Change);
                m_pltlabsapi.subscribe(PLTService.WEARING_STATE_SVC, PLTMode.On_Change);
            }
        }

        public void DeviceAdded(PLTDevice pltDevice)
        {
            if (!m_pltlabsapi.getIsConnected(pltDevice))
            {
                m_pltlabsapi.openConnection(pltDevice);
            }
        }

        public void infoUpdated(PLTConnection pltConnection, PLTInfo pltInfo)
        {
            if (pltInfo != null && pltInfo.m_data != null)
            {
                switch (pltInfo.m_serviceType)
                {
                    case PLTService.SENSOR_CAL_STATE_SVC:
                        PLTSensorCal caldata = (PLTSensorCal)pltInfo.m_data;
                        m_calibrated = caldata.m_isgyrocal;
                        break;
                    case PLTService.MOTION_TRACKING_SVC:
                        PLTMotionTrackingData motiondata = (PLTMotionTrackingData)pltInfo.m_data;
                        HeadsetTrackingUpdate(motiondata);
                        break;
                    case PLTService.WEARING_STATE_SVC:
                        PLTWearingState weardata = (PLTWearingState)pltInfo.m_data;
                        m_worn = weardata.m_worn;
                        if (weardata.m_worn && !weardata.m_isInitialStateEvent)
                        {
                            // headset was put on
                            // lets auto calibrate
                            m_autoputoncalibratetimer.Start();
                        }
                        break;
                }
            }
        }
    }
}
