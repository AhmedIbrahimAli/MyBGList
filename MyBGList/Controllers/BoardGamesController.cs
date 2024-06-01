using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyBGList.Attributes;
using MyBGList.Constants;
using MyBGList.DTO;
using MyBGList.Models;
using System.ComponentModel.DataAnnotations;
using System.Linq.Dynamic.Core;
using System.Text.Json;

namespace MyBGList.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class BoardGamesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BoardGamesController> _logger;
        private readonly IMemoryCache _memoryCache;
        public BoardGamesController(
            ApplicationDbContext context,
            ILogger<BoardGamesController> logger,
            IMemoryCache memoryCache)
        {
            _context = context;
            _logger = logger;
            _memoryCache = memoryCache;

        }

        [HttpGet("{id}")]
        public async Task<RestDTO<BoardGame?>> GetById(int id)
        {
            BoardGame? result = null;

            var cacheKey = $"GetBoardGame-{id}";
            if (!_memoryCache.TryGetValue<BoardGame>(cacheKey, out result))
            {
                result = await _context.BoardGames.FirstOrDefaultAsync(bg => bg.Id == id);
                _memoryCache.Set(cacheKey, result, new TimeSpan(0, 0, 30));
            }
            return new RestDTO<BoardGame?>()
            {
                Data = result,
                PageIndex = 0,
                PageSize = 1,
                RecordCount = result != null ? 1 : 0,
                Links = new List<LinkDTO>
                {
                    new LinkDTO(
                        Url.Action(null , "BoardGames" ,new {id} ,Request.Scheme)!,
                        "self",
                        "Get"

                        ),
                }

            };
             
        }

        [HttpGet(Name = "GetBoardGames")] 
        // set the public Caching with max age time
        [ResponseCache(Location = ResponseCacheLocation.Any, Duration = 60)]
        public async Task<RestDTO<BoardGame[]>> Get([FromQuery] RequestDTO<BoardGameDTO> input)
        {
            //Creates the IQueryable<T> expression tree
            // Filtering
            var query = _context.BoardGames.AsQueryable();
                if (!string.IsNullOrEmpty(input.FilterQuery)) 
                    query = query.Where(b => b.Name.Contains(input.FilterQuery));

            var recordCount = await query.CountAsync();

            // Caching
            BoardGame[]? result = null;
            var cacheKey = $"{input.GetType()}-{JsonSerializer.Serialize(input)}";
            if (!_memoryCache.TryGetValue<BoardGame[]>(cacheKey, out result))
            {
                query = query
                 .OrderBy($"{input.SortColumn} {input.SortOrder}")
                 .Skip(input.PageIndex * input.PageSize)
                 .Take(input.PageSize);
                result = await query.ToArrayAsync();
                _memoryCache.Set(cacheKey, result, new TimeSpan(0, 0, 30));

            }
               

            return new RestDTO<BoardGame[]>()
            {
                //Data = new BoardGame[]
                //    {
                //    new BoardGame()
                //     {
                //         Id = 1,
                //         Name = "Axis & Allies",
                //         Year = 1981,

                //     },
                //     new BoardGame()
                //     {
                //         Id = 2,
                //         Name = "Citadels",
                //         Year = 2000,

                //     },
                //     new BoardGame()
                //     {
                //         Id = 3,
                //         Name = "Terraforming Mars",
                //         Year = 2016,
                //     }
                //},

                //Executes the IQueryable<T>
                Data = result,
                PageIndex = input.PageIndex,
                PageSize = input.PageSize,
                //RecordCount = await _context.BoardGames.CountAsync(),
                RecordCount = recordCount,

                Links = new List<LinkDTO>
               {
                   new LinkDTO(
                    Url.Action(null, "BoardGames", new { input.PageIndex, input.PageSize }, Request.Scheme)!,
                    "self",
                    "GET"),
               }
               
            };
        }


        [Authorize(Roles = RoleNames.Moderator)]
        [HttpPost(Name = "UpdateBoardGame")]
        [ResponseCache(NoStore = true)]
        
        public async Task<RestDTO<BoardGame?>> Post(BoardGameDTO model)
        {
           var boardgame = await _context.BoardGames
                .Where(b => b.Id == model.Id)
                .FirstOrDefaultAsync();

            if (boardgame != null)
            {
                if (!string.IsNullOrEmpty(model.Name))
                    boardgame.Name = model.Name;
                if (model.Year.HasValue && model.Year.Value > 0)
                    boardgame.Year = model.Year.Value;
                boardgame.LastModifiedDate = DateTime.Now;
                _context.BoardGames.Update(boardgame);
                await _context.SaveChangesAsync();
                    

            }
            return new RestDTO<BoardGame?>()
            {
                Data = boardgame,
                Links= new List<LinkDTO>
                {
                    new LinkDTO(
                        Url.Action(null ,"BoardGames" ,model ,Request.Scheme)!,
                        "self",
                        "POST"
                        )
                }
            };
        }


        [Authorize(Roles = RoleNames.Administrator)]
        [HttpDelete(Name = "DeleteBoardGame")]
        [ResponseCache(NoStore = true)]
        
        public async Task<RestDTO<BoardGame?>> Delete(int id)
        {
            var boardgame =await _context.BoardGames
                .Where(b => b.Id == id)
                .FirstOrDefaultAsync();

            if (boardgame != null)
            {
                _context.BoardGames.Remove(boardgame);
                await _context.SaveChangesAsync();
            }

            return new RestDTO<BoardGame?>()
            {
                Data = boardgame,
                Links = new List<LinkDTO>
                {
                    new LinkDTO
                    (
                    Url.Action(null , "BoardGa" , id , Request.Scheme)!,
                    "Self",
                    "DELETE"
                    )
                }
            };
        }
    }
}
