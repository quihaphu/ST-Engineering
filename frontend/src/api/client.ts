export type Category = {
  id: string
  name: string
}

export type AttributeDefinition = {
  id: string
  name: string
  dataType: string
}

export type ProductVariant = {
  id?: string | null
  sku: string
  size: string
  color: string
  price: number
  stockQuantity: number
}

export type ProductAttributeValue = {
  attributeDefinitionId: string
  attributeName?: string | null
  value: string
}

export type ProductListItem = {
  id: string
  name: string
  categoryName: string
  minPrice: number | null
  totalStock: number
  variantCount: number
}

export type PagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export type ProductDetail = {
  id: string
  name: string
  description?: string | null
  categoryId: string
  categoryName: string
  rowVersion: string
  variants: ProductVariant[]
  attributes: ProductAttributeValue[]
}

export type ProductWritePayload = {
  name: string
  description?: string | null
  categoryId: string
  variants: ProductVariant[]
  attributes?: ProductAttributeValue[]
  rowVersion?: string
}

export class ApiError extends Error {
  status: number
  errors?: Record<string, string[]>
  title: string

  constructor(status: number, title: string, errors?: Record<string, string[]>) {
    super(title)
    this.status = status
    this.title = title
    this.errors = errors
  }
}

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
    ...init,
  })

  if (response.status === 204) {
    return undefined as T
  }

  const text = await response.text()
  const body = text ? JSON.parse(text) : null

  if (!response.ok) {
    throw new ApiError(
      response.status,
      body?.title ?? response.statusText,
      body?.errors,
    )
  }

  return body as T
}

export function getProducts(params: {
  page: number
  pageSize: number
  search?: string
  categoryId?: string
}) {
  const qs = new URLSearchParams()
  qs.set('page', String(params.page))
  qs.set('pageSize', String(params.pageSize))
  if (params.search) qs.set('search', params.search)
  if (params.categoryId) qs.set('categoryId', params.categoryId)
  return request<PagedResult<ProductListItem>>(`/api/products?${qs}`)
}

export function getProduct(id: string) {
  return request<ProductDetail>(`/api/products/${id}`)
}

export function createProduct(payload: ProductWritePayload) {
  return request<ProductDetail>('/api/products', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export function updateProduct(id: string, payload: ProductWritePayload) {
  return request<ProductDetail>(`/api/products/${id}`, {
    method: 'PUT',
    body: JSON.stringify(payload),
  })
}

export function deleteProduct(id: string) {
  return request<void>(`/api/products/${id}`, { method: 'DELETE' })
}

export function getCategories() {
  return request<Category[]>('/api/categories')
}

export function getAttributeDefinitions() {
  return request<AttributeDefinition[]>('/api/attribute-definitions')
}
