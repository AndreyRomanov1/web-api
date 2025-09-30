using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : Controller
{
    private readonly IUserRepository userRepository;
    private readonly IMapper mapper;

    public UsersController(IUserRepository userRepository, IMapper mapper)
    {
        this.userRepository = userRepository;
        this.mapper = mapper;
    }

    [HttpGet("{userId:guid}", Name = nameof(GetUserById))]
    [HttpHead("{userId:guid}")]
    [Produces("application/json", "application/xml")]
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
    [Produces("application/json", "application/xml")]
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
            insertedUserEntity.Id );
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

    [HttpOptions]
    public IActionResult GetUsersOptions()
    {
        Response.Headers.Append("Allow", "POST, GET, OPTIONS");
        return Ok();
    }

    private void CheckLogin(string? login)
    {
        if (!string.IsNullOrEmpty(login) && !login.All(char.IsLetterOrDigit))
        {
            ModelState.AddModelError("Login", "Login can contain only letters and digits");
        }
    }
}