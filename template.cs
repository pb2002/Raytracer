using System;
using System.Drawing;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

// The template provides you with a window which displays a 'linear frame buffer', i.e.
// a 1D array of pixels that represents the graphical contents of the window.

// Under the hood, this array is encapsulated in a 'Surface' object, and copied once per
// frame to an OpenGL texture, which is then used to texture 2 triangles that exactly
// cover the window. This is all handled automatically by the template code.

// Before drawing the two triangles, the template calls the Tick method in MyApplication,
// in which you are expected to modify the contents of the linear frame buffer.

// After (or instead of) rendering the triangles you can add your own OpenGL code.

// We will use both the pure pixel rendering as well as straight OpenGL code in the
// tutorial. After the tutorial you can throw away this template code, or modify it at
// will, or maybe it simply suits your needs.

namespace Template
{
	public class OpenTkApp : GameWindow
	{
		private static int _screenID;            // unique integer identifier of the OpenGL texture
		private static MyApplication _app;       // instance of the application
		private static bool _terminated = false; // application terminates gracefully when this is true

		protected override void OnLoad( EventArgs e )
		{
			// called during application initialization
			GL.Hint( HintTarget.PerspectiveCorrectionHint, HintMode.Nicest );
			ClientSize = new Size( AppSettings.ViewportWidth, AppSettings.ViewportHeight );
			_app = new MyApplication();
			_app.Screen = new Surface( Width, Height );
			Sprite.Target = _app.Screen;
			_screenID = _app.Screen.GenTexture();
			_app.Init();
		}
		protected override void OnUnload( EventArgs e )
		{
			// called upon app close
			GL.DeleteTextures( 1, ref _screenID );
			Environment.Exit( 0 );      // bypass wait for key on CTRL-F5
		}
		protected override void OnResize( EventArgs e )
		{
			// called upon window resize. Note: does not change the size of the pixel buffer.
			GL.Viewport( 0, 0, Width, Height );
			GL.MatrixMode( MatrixMode.Projection );
			GL.LoadIdentity();
			GL.Ortho( -1.0, 1.0, -1.0, 1.0, 0.0, 4.0 );
		}
		protected override void OnUpdateFrame( FrameEventArgs e )
		{
			// called once per frame; app logic
			var keyboard = OpenTK.Input.Keyboard.GetState();
			if( keyboard[OpenTK.Input.Key.Escape] ) _terminated = true;
		}
		protected override void OnRenderFrame( FrameEventArgs e )
		{
			// called once per frame; render
			_app.Tick();
			if (_terminated)
			{
				Exit();
				return;
			}

			GL.ClearColor( Color.Black );
			GL.Enable( EnableCap.Texture2D );
			GL.Disable( EnableCap.DepthTest );
			GL.Color3( 1.0f, 1.0f, 1.0f );
			
			// disable shaders
			// https://community.khronos.org/t/disabling-glsl-shader-program-targets-independently/53133/6
			GL.UseProgram(0);

			// convert MyApplication.screen to OpenGL texture
			GL.BindTexture( TextureTarget.Texture2D, _screenID );
			GL.TexImage2D( TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
						   _app.Screen.Width, _app.Screen.Height, 0,
						   PixelFormat.Bgra,
						   PixelType.UnsignedByte, _app.Screen.Pixels
						 );
			// draw screen filling quad
			// adjusted quad coords and tex coords to fill only half the screen
			GL.Begin( PrimitiveType.Quads );
			GL.TexCoord2( 0.0f, 1.0f ); GL.Vertex2( -1.0f, -1.0f );
			GL.TexCoord2( 0.5f, 1.0f ); GL.Vertex2( 0.0f, -1.0f );
			GL.TexCoord2( 0.5f, 0.0f ); GL.Vertex2( 0.0f, 1.0f );
			GL.TexCoord2( 0.0f, 0.0f ); GL.Vertex2( -1.0f, 1.0f );
			GL.End();
			GL.Clear(ClearBufferMask.DepthBufferBit);
			_app.OnRender();
			
			// tell OpenTK we're done rendering
			SwapBuffers();
		}
		public static void Main( string[] args )
		{
			// entry point
			using (OpenTkApp app = new OpenTkApp())
			{
				app.Title = "MARBLE Engine";
				app.Run( 30.0, 0.0 );
			}
		}
	}
}