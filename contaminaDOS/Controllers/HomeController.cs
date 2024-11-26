using Microsoft.AspNetCore.Mvc;
using Core.Models.Types;
using Core.Models.Business;

[ApiController]
[Route("/")]
public class HomeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        string htmlContent = @"
            <html>
                <head>
                    <title>ContaminaDOS G.E</title>
                    <style>
                        body {
                            font-family: Arial, sans-serif;
                            display: flex;
                            justify-content: center;
                            align-items: center;
                            height: 100vh;
                            margin: 0;
                            background-color: #1a1a1a;
                            color: #f4f4f4;
                            text-align: center;
                        }
                        h1 {
                            font-size: 3rem;
                            margin: 0;
                        }
                        p {
                            font-size: 1.5rem;
                            margin: 0;
                        }
                    </style>
                </head>
                <body>
                    <div>
                        <h1>ContaminaDOS</h1>
                        <p>Welcome to ContaminaDOS Group E</p>
                    </div>
                </body>
            </html>";
        
        return Content(htmlContent, "text/html");
    }
}