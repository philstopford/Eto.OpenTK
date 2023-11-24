using System;
using System.Diagnostics;
using Eto.Forms;
using Eto.OpenTK;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
#if OPENTK4
using OpenTK.Mathematics;
using OpenTK.Audio.OpenAL;
#endif

namespace TestEtoOpenTK
{
	public class SimpleView : GLSurface
	{
		IBindingsContext bc;

		// In NDC, (0, 0) is the center of the screen.
		// Negative X coordinates move to the left, positive X move to the right.
		// Negative Y coordinates move to the bottom, positive Y move to the top.
		// OpenGL only supports rendering in 3D, so to create a flat triangle, the Z coordinate will be kept as 0.
		private readonly float[] _vertices =
		{
			-0.5f, -0.5f, 0.0f, // Bottom-left vertex
			0.5f, -0.5f, 0.0f, // Bottom-right vertex
			0.0f,  0.5f, 0.0f  // Top vertex
		};

		// These are the handles to OpenGL objects. A handle is an integer representing where the object lives on the
		// graphics card. Consider them sort of like a pointer; we can't do anything with them directly, but we can
		// send them to OpenGL functions that need them.

		// What these objects are will be explained in OnLoad.
		private int _vertexBufferObject;

		private int _vertexArrayObject;

		// This class is a wrapper around a shader, which helps us manage it.
		// The shader class's code is in the Common project.
		// What shaders are and what they're used for will be explained later in this tutorial.
		private Shader _shader;

		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);
			// This will be the color of the background after we clear it, in normalized colors.
            // Normalized colors are mapped on a range of 0.0 to 1.0, with 0.0 representing black, and 1.0 representing
            // the largest possible value for that channel.
            // This is a deep green.
            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

            // We need to send our vertices over to the graphics card so OpenGL can use them.
            // To do this, we need to create what's called a Vertex Buffer Object (VBO).
            // These allow you to upload a bunch of data to a buffer, and send the buffer to the graphics card.
            // This effectively sends all the vertices at the same time.

            // First, we need to create a buffer. This function returns a handle to it, but as of right now, it's empty.
            _vertexBufferObject = GL.GenBuffer();

            // Now, bind the buffer. OpenGL uses one global state, so after calling this,
            // all future calls that modify the VBO will be applied to this buffer until another buffer is bound instead.
            // The first argument is an enum, specifying what type of buffer we're binding. A VBO is an ArrayBuffer.
            // There are multiple types of buffers, but for now, only the VBO is necessary.
            // The second argument is the handle to our buffer.
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);

            // Finally, upload the vertices to the buffer.
            // Arguments:
            //   Which buffer the data should be sent to.
            //   How much data is being sent, in bytes. You can generally set this to the length of your array, multiplied by sizeof(array type).
            //   The vertices themselves.
            //   How the buffer will be used, so that OpenGL can write the data to the proper memory space on the GPU.
            //   There are three different BufferUsageHints for drawing:
            //     StaticDraw: This buffer will rarely, if ever, update after being initially uploaded.
            //     DynamicDraw: This buffer will change frequently after being initially uploaded.
            //     StreamDraw: This buffer will change on every frame.
            //   Writing to the proper memory space is important! Generally, you'll only want StaticDraw,
            //   but be sure to use the right one for your use case.
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            // One notable thing about the buffer we just loaded data into is that it doesn't have any structure to it. It's just a bunch of floats (which are actaully just bytes).
            // The opengl driver doesn't know how this data should be interpreted or how it should be divided up into vertices. To do this opengl introduces the idea of a 
            // Vertex Array Obejct (VAO) which has the job of keeping track of what parts or what buffers correspond to what data. In this example we want to set our VAO up so that 
            // it tells opengl that we want to interpret 12 bytes as 3 floats and divide the buffer into vertices using that.
            // To do this we generate and bind a VAO (which looks deceptivly similar to creating and binding a VBO, but they are different!).
            _vertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(_vertexArrayObject);

            // Now, we need to setup how the vertex shader will interpret the VBO data; you can send almost any C datatype (and a few non-C ones too) to it.
            // While this makes them incredibly flexible, it means we have to specify how that data will be mapped to the shader's input variables.

            // To do this, we use the GL.VertexAttribPointer function
            // This function has two jobs, to tell opengl about the format of the data, but also to associate the current array buffer with the VAO.
            // This means that after this call, we have setup this attribute to source data from the current array buffer and interpret it in the way we specified.
            // Arguments:
            //   Location of the input variable in the shader. the layout(location = 0) line in the vertex shader explicitly sets it to 0.
            //   How many elements will be sent to the variable. In this case, 3 floats for every vertex.
            //   The data type of the elements set, in this case float.
            //   Whether or not the data should be converted to normalized device coordinates. In this case, false, because that's already done.
            //   The stride; this is how many bytes are between the last element of one vertex and the first element of the next. 3 * sizeof(float) in this case.
            //   The offset; this is how many bytes it should skip to find the first element of the first vertex. 0 as of right now.
            // Stride and Offset are just sort of glossed over for now, but when we get into texture coordinates they'll be shown in better detail.
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            // Enable variable 0 in the shader.
            GL.EnableVertexAttribArray(0);
            
            // We've got the vertices done, but how exactly should this be converted to pixels for the final image?
            // Modern OpenGL makes this pipeline very free, giving us a lot of freedom on how vertices are turned to pixels.
            // The drawback is that we actually need two more programs for this! These are called "shaders".
            // Shaders are tiny programs that live on the GPU. OpenGL uses them to handle the vertex-to-pixel pipeline.
            // Check out the Shader class in Common to see how we create our shaders, as well as a more in-depth explanation of how shaders work.
            // shader.vert and shader.frag contain the actual shader code.
            _shader = new Shader("Shaders/shader.vert", "Shaders/shader.frag");

            // Now, enable the shader.
            // Just like the VBO, this is global, so every function that uses a shader will modify this one until a new one is bound instead.
            _shader.Use();

            // Setup is now complete! Now we move to the OnRenderFrame function to finally draw the triangle.			
		}
		/*
		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);
			GL.Enable(EnableCap.Blend);
			GL.Enable(EnableCap.DepthTest);
			// GL.Enable(EnableCap.ScissorTest);
		}

		protected override void OnDraw(EventArgs e)
		{
			base.OnDraw(e);

			var hue = (float)_stopwatch.Elapsed.TotalSeconds * 0.15f % 1;
			var c = Color4.FromHsv(new Vector4(hue, 0.75f, 0.75f, 1));
			GL.ClearColor(c);
			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
			GL.LoadIdentity();
			GL.Begin(PrimitiveType.Triangles);

			GL.Color4(Color4.Red);
			GL.Vertex2(0.0f, 0.5f);

			GL.Color4(Color4.Green);
			GL.Vertex2(0.58f, -0.5f);

			GL.Color4(Color4.Blue);
			GL.Vertex2(-0.58f, -0.5f);

			GL.End();
			GL.Finish();
			Application.Instance.AsyncInvoke(Invalidate);
		}
		*/
	}
}