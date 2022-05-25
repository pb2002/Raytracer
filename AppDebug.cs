using System;
using System.Diagnostics;
using OpenTK;

namespace Template
{
    public partial class MyApplication
    {
        private float debugUnitScale = spawnFieldSize * 1.5f;
        private Stopwatch fpsCounter = new Stopwatch();
        private float frameTime;
        
        private int ConvertColor(Vector3 c)
        {
            byte r = (byte) Math.Min(255, (c.X * 255));
            byte g = (byte) Math.Min(255, (c.Y * 255));
            byte b = (byte) Math.Min(255, (c.Z * 255));
            return b + (g << 8) + (r << 16);
        }
        
        Vector2 WorldToDebug(Vector3 v)
        {
            // flatten the height component of the vector
            Vector2 result = v.Xz;
            
            // rescale
            float scale = screen.height / debugUnitScale;
            result *= scale;
            
            // offset
            result += Vector2.One * screen.height / 2f;
            
            return result;
        }
        
        // function for drawing a sphere graphic in the debug window
        void DrawDebugSphere(Sphere s)
        {
            int color = ConvertColor(s.mat.color);
            Vector2 center = WorldToDebug(s.position);
            float radius = s.radius * screen.height / debugUnitScale;
            Vector2 start = center + new Vector2(radius, 0);
            
            // draw 50 line segments
            for (float theta = 0; theta < 2 * (float) Math.PI; theta += (float) Math.PI / 25f)
            {
                Vector2 end = center + new Vector2(radius * (float) Math.Cos(theta), radius * (float) Math.Sin(theta));
                screen.Line((int)start.X, (int)start.Y, (int)end.X, (int)end.Y, color);
                start = end;
            }
        }
        
        // function for drawing the raytracing graphic in the debug window
        // because spheres rarely align with y = width / 2, the method described in the lecture
        // will not work with our raytracer.
        void DrawDebugRays()
        {
            // starting point (camera position)
            Vector2 start = WorldToDebug(camera.Position);
            
            // take samples along y = width / 2
            for (int x = 0; x < screen.width / 2; x += 40)
            {   
                // trace ray
                Ray r = camera.CreateCameraRay(x, 0);
                Intersection hit = scene.Intersect(r);
                
                // get the intersection point
                Vector3 point;
                if (float.IsPositiveInfinity(hit.dst))
                {
                    // lines bigger than 2 * sqrt(2) * debugUnitScale will always exceed screen bounds
                    // 3 > 2 * sqrt(2) so 3 * debugUnitScale will have us covered.
                    point = r.origin + 3f * debugUnitScale * r.direction;
                }
                else
                {
                    point = hit.point;
                }

                // convert to debug coordinate space
                Vector2 end = WorldToDebug(point);
                
                // draw line
                screen.Line((int)start.X, (int)start.Y, (int)end.X, (int)end.Y, 0x00FFFF00);    
            }
            
        }
        void Debug()
        {
            // draw the spheres
            foreach (var primitive in scene.spheres)
            {
                if (primitive is Sphere s)
                {
                    DrawDebugSphere(s);
                }
            }
            DrawDebugRays();
            
            // draw the camera graphic
            Vector2 camPos = WorldToDebug(camera.Position);
            Vector2 imageLeft = WorldToDebug(camera.TopLeft);
            Vector2 imageRight = WorldToDebug(camera.TopRight);
            screen.Box((int)camPos.X-5, (int)camPos.Y-5, (int)camPos.X+5, (int)camPos.Y+5,0x00ffffff);
            screen.Line((int)imageLeft.X, (int)imageLeft.Y, (int)imageRight.X, (int)imageRight.Y, 0x00ffffff);
            
            // draw the text elements
            float diagonal = camera.AspectRatio * camera.AspectRatio + 1;
            diagonal = (float) Math.Sqrt(diagonal);
            
            float fov = (float) (360 / Math.PI * Math.Atan(diagonal / (2 * camera.FocalLength)));
            
            screen.Print("WASD to move, QERF to rotate, Z/X for viewing distance", 10, 10, 0x00FFFFFF);
            screen.Print($"FOV (+/-): {fov:F2}", 10, 30, 0x00FFFFFF);
            screen.Print($"Tonemapping (T): {(enableTonemapping ? "ON" : "OFF")}", 10, 50, 0x00FFFFFF);
            if (enableTonemapping) screen.Print($"Exposure bias ([ and ]): {exposureBias:F2}", 260, 50, 0x00FFFFFF);
            
            // draw the fps counter
            float fps = 0;
            if (fpsCounter.IsRunning)
            {
                fpsCounter.Stop();
                frameTime = (float) fpsCounter.Elapsed.TotalSeconds;
                fps = 1f / frameTime;
                fpsCounter.Restart();
            }
            else
            {
                fpsCounter.Start();
            }
            screen.Print($"FPS: {fps:F2}", 10, 80, 0x00FFFFFF);
        }
    }
}