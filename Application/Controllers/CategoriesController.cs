using DataLabelProject.Application.DTOs.Categories;
using DataLabelProject.Application.DTOs.Labels;
using DataLabelProject.Application.DTOs.Projects;
using DataLabelProject.Business.Services.Categories;
using DataLabelProject.Business.Services.Labels;
using DataLabelProject.Business.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataLabelProject.Application.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly IProjectService _projectService;
    private readonly ILabelService _labelService;

    public CategoriesController(
        ICategoryService categoryService,
        IProjectService projectService,
        ILabelService labelService)
    {
        _categoryService = categoryService;
        _projectService = projectService;
        _labelService = labelService;
    }

    [HttpGet]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> GetAll([FromQuery] CategoryQueryParameters @params) 
    {
        var result = await _categoryService.GetCategories(@params);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _categoryService.GetCategoryById(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id}/projects")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> GetCategoryProjects(
        [FromRoute] Guid id,
        [FromQuery] ProjectQueryParameters @params)
    {
        var result = await _projectService.GetCategoryProjects(id, @params);
        return Ok(result);
    }

    [HttpGet("{id}/labels")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> GetCategoryLabels(
        [FromRoute] Guid id,
        [FromQuery] LabelQueryParameters @params)
    {
        var result = await _labelService.GetCategoryLabels(id, @params);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
    {
        var result = await _categoryService.CreateCategory(request);
        return CreatedAtAction(nameof(GetById), new { id = result.CategoryId }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        var result = await _categoryService.UpdateCategory(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin,manager")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _categoryService.DeactivateCategory(id);
        return NoContent();
    }
}
