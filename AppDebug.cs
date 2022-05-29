using System;
using System.Diagnostics;
using OpenTK;

namespace Template
{
    public partial class MyApplication
    {
        private bool _debugMode;
        
        private int ConvertColor(Vector3 c)
        {
            byte r = (byte) Math.Min(255, (c.X * 255));
            byte g = (byte) Math.Min(255, (c.Y * 255));
            byte b = (byte) Math.Min(255, (c.Z * 255));
            return b + (g << 8) + (r << 16);
        }

        private Vector2 WorldToDebug(Vector3 v)
        {
            // flatten the height component of the vector
            Vector2 result = v.Xz;
            
            // rescale
            float scale = Screen.Height / AppSettings.DebugUnitScale;
            result *= scale;
            
            // offset
            result += Vector2.One * Screen.Height / 2f;
            
            return result;
        }
        
        // function for drawing a sphere graphic in the debug window
        private void DrawDebugSphere(Sphere s)
        {
            int color = ConvertColor(s.Material.Color);
            Vector2 center = WorldToDebug(s.Position);
            float radius = s.Radius * Screen.Height / AppSettings.DebugUnitScale;
            Vector2 start = center + new Vector2(radius, 0);
            Vector2 circleStart = start;
            // draw 50 line segments
            for (float theta = 0; theta < 2 * (float) Math.PI; theta += (float) Math.PI / 25f)
            {
                Vector2 end = center + new Vector2(radius * (float) Math.Cos(theta), radius * (float) Math.Sin(theta));
                Screen.Line((int)start.X, (int)start.Y, (int)end.X, (int)end.Y, color);
                start = end;
            }
            // connect the ends together
            Screen.Line((int)start.X, (int)start.Y, (int)circleStart.X, (int)circleStart.Y, color);
        }
        
        // function for drawing the raytracing graphic in the debug window
        // because spheres rarely align with y = width / 2, the method described in the lecture
        // will not work with our raytracer.
        private void DrawDebugRays()
        {
            // starting point (camera position)
            Vector2 start = WorldToDebug(_camera.Position);
            
            // take samples along y = width / 2
            for (int x = 0; x < Screen.Width / 2; x += 40)
            {   
                // trace ray
                Ray r = _camera.CreateCameraRay(x, 0);
                Intersection hit = _scene.Intersect(r);
                
                // get the intersection point
                Vector3 point;
                if (float.IsPositiveInfinity(hit.Dst))
                {
                    // lines bigger than 2 * sqrt(2) * debugUnitScale will always exceed screen bounds
                    // 3 > 2 * sqrt(2) so 3 * debugUnitScale will have us covered.
                    point = r.Origin + 3f * AppSettings.DebugUnitScale * r.Direction;
                }
                else
                {
                    point = hit.Point;
                }

                // convert to debug coordinate space
                Vector2 end = WorldToDebug(point);
                
                // draw line
                Screen.Line((int)start.X, (int)start.Y, (int)end.X, (int)end.Y, 0x00FFFF00);    
            }
            
        }

        private void DrawDebug()
        {
            if (_debugMode)
            {
                // draw the text elements
                float diagonal = _camera.AspectRatio * _camera.AspectRatio + 1;
                diagonal = (float) Math.Sqrt(diagonal);
            
                float fov = (float) (360 / Math.PI * Math.Atan(diagonal / (2 * _camera.FocalLength)));
                float fps = 1f / _frameTime;

                Screen.Print($"FOV (+ and -): {fov:F2}", 10, 10, 0x00FFFFFF);
                Screen.Print($"Tonemapping (T): {(AppSettings.UseTonemapping ? "ON" : "OFF")}", 10, 30, 0x00FFFFFF);
                if (AppSettings.UseTonemapping) Screen.Print($"Exposure bias ([ and ]): {AppSettings.ExposureBias:F2}", 10, 50, 0x00FFFFFF);
                Screen.Print($"FPS: {fps:F0}", 10, 70, 0x008888FF);
                Screen.Print($"SSBO: {AppSettings.PrimitiveBufferSize/256}KB", 120, 70, 0x008888FF);

                Screen.Print("Move: WASD", 10, Screen.Height - 70, 0x00FFFFFF);
                Screen.Print("Rotate: QERF", 10, Screen.Height - 50, 0x00FFFFFF);
                Screen.Print("Viewing distance: Z/X", 10, Screen.Height - 30, 0x00FFFFFF);
            }
            else
            {
                Screen.Print("Press Tab to toggle debug info", 10, 10, 0x00FFFFFF);
                // draw the spheres
                foreach (var primitive in _scene.Spheres)
                {
                    if (primitive is Sphere s)
                    {
                        DrawDebugSphere(s);
                    }
                }
                DrawDebugRays();
            
                // draw the camera graphic
                Vector2 camPos = WorldToDebug(_camera.Position);
                Vector2 imageLeft = WorldToDebug(_camera.TopLeft);
                Vector2 imageRight = WorldToDebug(_camera.TopRight);
                Screen.Box((int)camPos.X-5, (int)camPos.Y-5, (int)camPos.X+5, (int)camPos.Y+5,0x00ffffff);
                Screen.Line((int)imageLeft.X, (int)imageLeft.Y, (int)imageRight.X, (int)imageRight.Y, 0x00ffffff);   
            }
            
            
            
            
        }
    }
}