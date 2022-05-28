using System;
using System.Diagnostics;
using OpenTK;

namespace Template
{
    public partial class MyApplication
    {
        private bool debugMode;
        
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
            float scale = screen.height / AppSettings.debugUnitScale;
            result *= scale;
            
            // offset
            result += Vector2.One * screen.height / 2f;
            
            return result;
        }
        
        // function for drawing a sphere graphic in the debug window
        void DrawDebugSphere(Sphere s)
        {
            int color = ConvertColor(s.material.color);
            Vector2 center = WorldToDebug(s.position);
            float radius = s.radius * screen.height / AppSettings.debugUnitScale;
            Vector2 start = center + new Vector2(radius, 0);
            Vector2 circleStart = start;
            // draw 50 line segments
            for (float theta = 0; theta < 2 * (float) Math.PI; theta += (float) Math.PI / 25f)
            {
                Vector2 end = center + new Vector2(radius * (float) Math.Cos(theta), radius * (float) Math.Sin(theta));
                screen.Line((int)start.X, (int)start.Y, (int)end.X, (int)end.Y, color);
                start = end;
            }
            // connect the ends together
            screen.Line((int)start.X, (int)start.Y, (int)circleStart.X, (int)circleStart.Y, color);
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
                    point = r.origin + 3f * AppSettings.debugUnitScale * r.direction;
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
        void DrawDebug()
        {
            if (debugMode)
            {
                // draw the text elements
                float diagonal = camera.AspectRatio * camera.AspectRatio + 1;
                diagonal = (float) Math.Sqrt(diagonal);
            
                float fov = (float) (360 / Math.PI * Math.Atan(diagonal / (2 * camera.FocalLength)));
                float fps = 1f / frameTime;

                screen.Print($"FOV (+ and -): {fov:F2}", 10, 10, 0x00FFFFFF);
                screen.Print($"Tonemapping (T): {(AppSettings.UseTonemapping ? "ON" : "OFF")}", 10, 30, 0x00FFFFFF);
                if (AppSettings.UseTonemapping) screen.Print($"Exposure bias ([ and ]): {AppSettings.ExposureBias:F2}", 10, 50, 0x00FFFFFF);
                screen.Print($"FPS: {fps:F0}", 10, 70, 0x008888FF);
                screen.Print($"SSBO: {AppSettings.PrimitiveBufferSize/256}KB", 120, 70, 0x008888FF);

                screen.Print("Move: WASD", 10, screen.height - 70, 0x00FFFFFF);
                screen.Print("Rotate: QERF", 10, screen.height - 50, 0x00FFFFFF);
                screen.Print("Viewing distance: Z/X", 10, screen.height - 30, 0x00FFFFFF);
            }
            else
            {
                screen.Print("Press Tab to toggle debug info", 10, 10, 0x00FFFFFF);
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
            }
            
            
            
            
        }
    }
}