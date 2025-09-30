using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[Produces("application/json", "application/xml")]
public class UsersController : Controller
{
    private readonly IUserRepository userRepository;
    private readonly IMapper mapper;
    private readonly LinkGenerator linkGenerator;

    public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
    {
        this.userRepository = userRepository;
        this.mapper = mapper;
        this.linkGenerator = linkGenerator;
    }

    [HttpGet("{userId:guid}", Name = nameof(GetUserById))]
    [HttpHead("{userId:guid}")]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        var user = userRepository.FindById(userId);
        if (user == null)
        {
            return NotFound();
        }

        if (HttpMethods.IsHead(Request.Method))
        {
            return Content(string.Empty, "application/json; charset=utf-8");
        }

        return mapper.Map<UserDto>(user);
    }

    [HttpPost]
    public IActionResult CreateUser([FromBody] CreateUserDto? user)
    {
        if (user is null)
        {
            return BadRequest();
        }

        CheckLogin(user.Login);

        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        var createdUserEntity = mapper.Map<UserEntity>(user);
        var insertedUserEntity = userRepository.Insert(createdUserEntity);
        return CreatedAtRoute(
            nameof(GetUserById),
            new { userId = insertedUserEntity.Id },
            insertedUserEntity.Id);
    }

    [HttpDelete("{userId:guid}")]
    public IActionResult DeleteUser([FromRoute] Guid userId)
    {
        if (userRepository.FindById(userId) != null)
        {
            userRepository.Delete(userId);
            return NoContent();
        }

        return NotFound();
    }

    [HttpPut("{userId}")]
    public ActionResult UpdateUser([FromRoute] Guid userId, [FromBody] UpdateUserDto? dto)
    {
        if (userId == Guid.Empty || dto is null)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        var user = mapper.Map(dto, new UserEntity(userId));

        userRepository.UpdateOrInsert(user, out var isInserted);

        if (isInserted)
        {
            return CreatedAtRoute(
                nameof(GetUserById),
                new { userId = user.Id },
                user.Id);
        }

        return NoContent();
    }

    [HttpPatch("{userId}")]
    public ActionResult PartiallyUpdateUser([FromRoute] Guid userId, [FromBody] JsonPatchDocument<UpdateUserDto>? patchDoc)
    {
        if (userId == Guid.Empty)
        {
            return NotFound();
        }

        if (patchDoc is null)
        {
            return BadRequest();
        }

        var updateUserDto = new UpdateUserDto();
        patchDoc.ApplyTo(updateUserDto, ModelState);
        TryValidateModel(updateUserDto);
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        var existedUser = userRepository.FindById(userId);
        if (existedUser is null)
        {
            return NotFound();
        }

        var user = mapper.Map(updateUserDto, new UserEntity(userId));

        userRepository.Update(user);

        return NoContent();
    }

    private void CheckLogin(string? login)
    {
        if (!string.IsNullOrEmpty(login) && !login.All(char.IsLetterOrDigit))
        {
            ModelState.AddModelError("Login", "Login can contain only letters and digits");
        }
    }

    [HttpGet(Name = nameof(GetAllUsers))]
    [Produces("application/json", "application/xml")]
    public IActionResult GetAllUsers(GetUsersDto dto)
    {
        var usersPage = userRepository.GetPage(dto.pageNumber, dto.pageSize);
        var users = mapper.Map<IEnumerable<UserDto>>(usersPage);

        var paginationHeader = new
        {
            previousPageLink = usersPage.HasPrevious
                ? linkGenerator
                    .GetUriByRouteValues(HttpContext, "GetAllUsers",
                        new { pageNumber = dto.pageNumber - 1, pageSize = dto.pageSize })
                : null,
            nextPageLink = usersPage.HasNext
                ? linkGenerator
                    .GetUriByRouteValues(HttpContext, "GetAllUsers",
                        new { pageNumber = dto.pageNumber + 1, pageSize = dto.pageSize })
                : null,
            totalCount = usersPage.TotalCount,
            pageSize = usersPage.PageSize,
            currentPage = usersPage.CurrentPage,
            totalPages = usersPage.TotalPages,
        };
        Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(paginationHeader));

        return Ok(users);
    }
}